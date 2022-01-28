﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MonoMod.Cil;
using MonoMod.Utils;
using HarmonyLib;
using MelonLoader.TinyJSON;
using MelonLoader.InternalUtils;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
#pragma warning disable 0618

namespace MelonLoader
{
    public static class MelonUtils
    {
        internal static void Setup(AppDomain domain)
        {
            HashCode = string.Copy(Internal_GetHashCode());
            BaseDirectory = string.Copy(Internal_GetBaseDirectory());
            GameDirectory = string.Copy(Internal_GetGameDirectory());
            SetCurrentDomainBaseDirectory(GameDirectory, domain);

            UserDataDirectory = Path.Combine(BaseDirectory, "UserData");
            if (!Directory.Exists(UserDataDirectory))
                Directory.CreateDirectory(UserDataDirectory);

            UserLibsDirectory = Path.Combine(BaseDirectory, "UserLibs");
            if (!Directory.Exists(UserLibsDirectory))
                Directory.CreateDirectory(UserLibsDirectory);

            UnityInformationHandler.Setup();

            CurrentGameAttribute = new MelonGameAttribute(UnityInformationHandler.GameDeveloper, UnityInformationHandler.GameName);

            IsDemeo = (UnityInformationHandler.GameDeveloper.Equals("Resolution Games") && UnityInformationHandler.GameName.Equals("Demeo"));
            IsMuseDash = (UnityInformationHandler.GameDeveloper.Equals("PeroPeroGames") && UnityInformationHandler.GameName.Equals("Muse Dash"));
            IsBONEWORKS = (UnityInformationHandler.GameDeveloper.Equals("Stress Level Zero") && UnityInformationHandler.GameName.Equals("BONEWORKS"));
            Main.IsBoneworks = IsBONEWORKS;
        }

        public static string BaseDirectory { get; private set; }
        public static string GameDirectory { get; private set; }
        public static string UserDataDirectory { get; private set; }
        public static string UserLibsDirectory { get; private set; }
        public static MelonGameAttribute CurrentGameAttribute { get; private set; }
        public static bool IsBONEWORKS { get; private set; }
        public static bool IsDemeo { get; private set; }
        public static bool IsMuseDash { get; private set; }
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T> { if (value.CompareTo(min) < 0) return min; if (value.CompareTo(max) > 0) return max; return value; }
        public static string HashCode { get; private set; }

        public static string RandomString(int length)
        {
            StringBuilder builder = new StringBuilder();
            Random rand = new Random();
            for (int i = 0; i < length; i++)
                builder.Append(Convert.ToChar(Convert.ToInt32(Math.Floor(25 * rand.NextDouble())) + 65));
            return builder.ToString();
        }

        public static void SetCurrentDomainBaseDirectory(string dirpath, AppDomain domain = null)
        {
            if (domain == null)
                domain = AppDomain.CurrentDomain;
            try
            {
                ((AppDomainSetup)typeof(AppDomain).GetProperty("SetupInformationNoCopy", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(domain, new object[0]))
                    .SetApplicationBase(dirpath);
            }
            catch (Exception ex) { MelonLogger.Warning($"AppDomainSetup.ApplicationBase Exception: {ex}"); }
            Directory.SetCurrentDirectory(dirpath);
        }

        public static MelonBase GetMelonFromStackTrace()
        {
            StackTrace st = new StackTrace(3, true);
            if (st.FrameCount <= 0)
                return null;
            MelonBase output = CheckForMelonInFrame(st);
            if (output == null)
                output = CheckForMelonInFrame(st, 1);
            if (output == null)
                output = CheckForMelonInFrame(st, 2);
            return output;
        }
        private static MelonBase CheckForMelonInFrame(StackTrace st, int frame = 0)
        {
            StackFrame sf = st.GetFrame(frame);
            if (sf == null)
                return null;
            MethodBase method = sf.GetMethod();
            if (method == null)
                return null;
            Type methodClassType = method.DeclaringType;
            if (methodClassType == null)
                return null;
            Assembly asm = methodClassType.Assembly;
            if (asm == null)
                return null;
            MelonBase melon = MelonHandler.Plugins.Find(x => (x.Assembly == asm));
            if (melon == null)
                melon = MelonHandler.Mods.Find(x => (x.Assembly == asm));
            return melon;
        }

        public static string ColorToANSI(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Black => "\x1b[30m",
                ConsoleColor.DarkBlue => "\x1b[34m",
                ConsoleColor.DarkGreen => "\x1b[32m",
                ConsoleColor.DarkCyan => "\x1b[36m",
                ConsoleColor.DarkRed => "\x1b[31m",
                ConsoleColor.DarkMagenta => "\x1b[35m",
                ConsoleColor.DarkYellow => "\x1b[33m",
                ConsoleColor.Gray => "\x1b[37m",
                ConsoleColor.DarkGray => "\x1b[90m",
                ConsoleColor.Blue => "\x1b[94m",
                ConsoleColor.Green => "\x1b[92m",
                ConsoleColor.Cyan => "\x1b[96m",
                ConsoleColor.Red => "\x1b[91m",
                ConsoleColor.Magenta => "\x1b[95m",
                ConsoleColor.Yellow => "\x1b[93m",
                _ => "\x1b[97m",
            };
        }

        public static T ParseJSONStringtoStruct<T>(string jsonstr)
        {
            if (string.IsNullOrEmpty(jsonstr))
                return default;
            Variant jsonarr = null;
            try { jsonarr = JSON.Load(jsonstr); }
            catch (Exception ex)
            {
                MelonLogger.Error($"Exception while Decoding JSON String to JSON Variant: {ex}");
                return default;
            }
            if (jsonarr == null)
                return default;
            T returnobj = default;
            try { returnobj = jsonarr.Make<T>(); }
            catch (Exception ex) { MelonLogger.Error($"Exception while Converting JSON Variant to {typeof(T).Name}: {ex}"); }
            return returnobj;
        }

        public static T PullAttributeFromAssembly<T>(Assembly asm, bool inherit = false) where T : Attribute
        {
            T[] attributetbl = PullAttributesFromAssembly<T>(asm, inherit);
            if ((attributetbl == null) || (attributetbl.Length <= 0))
                return null;
            return attributetbl[0];
        }

        public static T[] PullAttributesFromAssembly<T>(Assembly asm, bool inherit = false) where T : Attribute
        {
            Attribute[] att_tbl = Attribute.GetCustomAttributes(asm, inherit);

            if ((att_tbl == null) || (att_tbl.Length <= 0))
                return null;

            Type requestedType = typeof(T);
            List<T> output = new List<T>();
            foreach (Attribute att in att_tbl)
            {
                Type attType = att.GetType();
                string attAssemblyName = attType.Assembly.GetName().Name;
                string requestedAssemblyName = requestedType.Assembly.GetName().Name;

                if ((attType == requestedType)
                    || attType.FullName.Equals(requestedType.FullName)
                    || ((attAssemblyName.Equals("MelonLoader")
                        || attAssemblyName.Equals("MelonLoader.ModHandler"))
                        && (requestedAssemblyName.Equals("MelonLoader")
                        || requestedAssemblyName.Equals("MelonLoader.ModHandler"))
                        && attType.Name.Equals(requestedType.Name)))
                    output.Add(att as T);
            }

            return output.ToArray();
        }

        public static IEnumerable<Type> GetValidTypes(this Assembly asm)
            => GetValidTypes(asm, null);
        public static IEnumerable<Type> GetValidTypes(this Assembly asm, LemonFunc<Type, bool> predicate)
        {
            IEnumerable<Type> returnval = Enumerable.Empty<Type>();
            try { returnval = asm.GetTypes().AsEnumerable(); }
            catch (ReflectionTypeLoadException ex) { returnval = ex.Types; }
            return returnval.Where(x =>
                ((x != null)
                    && ((predicate != null)
                        ? predicate(x)
                        : true)));
        }

        public static bool IsNotImplemented(this MethodBase methodBase)
        {
            if (methodBase == null)
                throw new ArgumentNullException(nameof(methodBase));

            DynamicMethodDefinition method = methodBase.ToNewDynamicMethodDefinition();
            ILContext ilcontext = new ILContext(method.Definition);
            ILCursor ilcursor = new ILCursor(ilcontext);

            bool returnval = (ilcursor.Instrs.Count == 2)
                && (ilcursor.Instrs[1].OpCode.Code == Mono.Cecil.Cil.Code.Throw);

            ilcontext.Dispose();
            method.Dispose();
            return returnval;
        }

        public static HarmonyMethod ToNewHarmonyMethod(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
                throw new ArgumentNullException(nameof(methodInfo));
            return new HarmonyMethod(methodInfo);
        }


        public static DynamicMethodDefinition ToNewDynamicMethodDefinition(this MethodBase methodBase)
        {
            if (methodBase == null)
                throw new ArgumentNullException(nameof(methodBase));
            return new DynamicMethodDefinition(methodBase);
        }

        private static FieldInfo AppDomainSetup_application_base;
        public static void SetApplicationBase(this AppDomainSetup _this, string value)
        {
            if (AppDomainSetup_application_base == null)
                AppDomainSetup_application_base = typeof(AppDomainSetup).GetField("application_base", BindingFlags.NonPublic | BindingFlags.Instance);
            if (AppDomainSetup_application_base != null)
                AppDomainSetup_application_base.SetValue(_this, value);
        }

        private static FieldInfo HashAlgorithm_HashSizeValue;
        public static void SetHashSizeValue(this HashAlgorithm _this, int value)
        {
            if (HashAlgorithm_HashSizeValue == null)
                HashAlgorithm_HashSizeValue = typeof(HashAlgorithm).GetField("HashSizeValue", BindingFlags.Public | BindingFlags.Instance);
            if (HashAlgorithm_HashSizeValue != null)
                HashAlgorithm_HashSizeValue.SetValue(_this, value);
        }

        // Modified Version of System.IO.Path.HasExtension from .NET Framework's mscorlib.dll
        public static bool ContainsExtension(this string path)
        {
            if (path != null)
            {
                path.CheckInvalidPathChars();
                int num = path.Length;
                while (--num >= 0)
                {
                    char c = path[num];
                    if (c == '.')
                        return num != path.Length - 1;
                    if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar || c == Path.VolumeSeparatorChar)
                        break;
                }
            }
            return false;
        }

        // Modified Version of System.IO.Path.CheckInvalidPathChars from .NET Framework's mscorlib.dll
        private static void CheckInvalidPathChars(this string path)
        {
            foreach (int num in path)
                if (num == 34 || num == 60 || num == 62 || num == 124 || num < 32)
                    throw new ArgumentException("Argument_InvalidPathChars", nameof(path));
        }

        public static void GetDelegate<T>(this IntPtr ptr, out T output) where T : Delegate
            => output = GetDelegate<T>(ptr);
        public static T GetDelegate<T>(this IntPtr ptr) where T : Delegate
            => GetDelegate(ptr, typeof(T)) as T;
        public static Delegate GetDelegate(this IntPtr ptr, Type type)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));
            Delegate del = Marshal.GetDelegateForFunctionPointer(ptr, type);
            if (del == null)
                throw new Exception($"Unable to Get Delegate of Type {type.FullName} for Function Pointer!");
            return del;
        }
        public static IntPtr GetFunctionPointer(this Delegate del)
            => Marshal.GetFunctionPointerForDelegate(del);

        public static NativeLibrary ToNewNativeLibrary(this IntPtr ptr)
            => new NativeLibrary(ptr);
        public static NativeLibrary<T> ToNewNativeLibrary<T>(this IntPtr ptr)
            => new NativeLibrary<T>(ptr);
        public static IntPtr GetNativeLibraryExport(this IntPtr ptr, string name)
            => NativeLibrary.GetExport(ptr, name);

        public static ClassDatabasePackage LoadIncludedClassPackage(this AssetsManager assetsManager)
        {
            ClassDatabasePackage classPackage = null;
            using (MemoryStream mstream = new MemoryStream(Properties.Resources.classdata))
                classPackage = assetsManager.LoadClassPackage(mstream);
            return classPackage;
        }

        public static ClassDatabasePackage LoadIncludedLargeClassPackage(this AssetsManager assetsManager)
        {
            ClassDatabasePackage classPackage = null;
            using (MemoryStream mstream = new MemoryStream(Properties.Resources.classdata_large))
                classPackage = assetsManager.LoadClassPackage(mstream);
            return classPackage;
        }


        [Obsolete("MelonLoader.MelonUtils.GetUnityVersion() is obsolete. Please use MelonLoader.InternalUtils.UnityInformationHandler.EngineVersion instead.")]
        public static string GetUnityVersion() => UnityInformationHandler.EngineVersion.ToStringWithoutType();
        [Obsolete("MelonLoader.MelonUtils.GameDeveloper is obsolete. Please use MelonLoader.InternalUtils.UnityInformationHandler.GameDeveloper instead.")]
        public static string GameDeveloper { get => UnityInformationHandler.GameDeveloper; }
        [Obsolete("MelonLoader.MelonUtils.GameName is obsolete. Please use MelonLoader.InternalUtils.UnityInformationHandler.GameName instead.")]
        public static string GameName { get => UnityInformationHandler.GameName; }
        [Obsolete("MelonLoader.MelonUtils.GameVersion is obsolete. Please use MelonLoader.InternalUtils.UnityInformationHandler.GameVersion instead.")]
        public static string GameVersion { get => UnityInformationHandler.GameVersion; }


        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static bool IsGame32Bit();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static bool IsGameIl2Cpp();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static bool IsOldMono();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static bool IsUnderWineOrSteamProton();
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public extern static string GetApplicationPath();
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public extern static string GetGameDataDirectory();
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public extern static string GetManagedDirectory();
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static void SetConsoleTitle([MarshalAs(UnmanagedType.LPStr)] string title);
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public extern static string GetFileProductName([MarshalAs(UnmanagedType.LPStr)] string filepath);

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static void NativeHookAttach(IntPtr target, IntPtr detour);
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern static void NativeHookDetach(IntPtr target, IntPtr detour);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private extern static string Internal_GetBaseDirectory();
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private extern static string Internal_GetGameDirectory();
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private extern static string Internal_GetHashCode();
    }
}