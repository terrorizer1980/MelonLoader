﻿using MelonLoader.Il2CppAssemblyGenerator.Packages;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace MelonLoader.Il2CppAssemblyGenerator
{

	internal static class Core
    {
        internal static string BasePath = null;
        internal static string GameAssemblyPath = null;
        internal static string ManagedPath = null;

        internal static WebClient webClient = null;

        internal static DumperBase dumper = null;
        internal static UnityDependencies unitydependencies = null;
        internal static DeobfuscationMap deobfuscationMap = null;
        internal static Il2CppAssemblyUnhollower il2cppassemblyunhollower = null;

        internal static bool AssemblyGenerationNeeded = false;

        static Core()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;

            webClient = new WebClient();
            webClient.Headers.Add("User-Agent", "Unity web player");

            AssemblyGenerationNeeded = MelonLaunchOptions.Il2CppAssemblyGenerator.ForceRegeneration;

            GameAssemblyPath = Path.Combine(MelonUtils.GameDirectory, "GameAssembly.dll");
            ManagedPath = string.Copy(MelonUtils.GetManagedDirectory());

            BasePath = Path.GetDirectoryName(typeof(Core).Assembly.Location);
        }

        private static int Run()
        {
            Config.Initialize();

            if (!MelonLaunchOptions.Il2CppAssemblyGenerator.OfflineMode)
                RemoteAPI.Contact();

            // Temporary Workaround for Cpp2IL Failing on Unsupported OSes
            if (!MelonUtils.IsUnderWineOrSteamProton() && ((Environment.OSVersion.Version.Major < 6) // Is Older than Vista
                || ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor < 1)))) // Is Older than Windows 7 or Server 2008 R2
                dumper = new Il2CppDumper();
            else
                dumper = new Cpp2IL();

            il2cppassemblyunhollower = new Il2CppAssemblyUnhollower();
            unitydependencies = new UnityDependencies();
            deobfuscationMap = new DeobfuscationMap();

            MelonLogger.Msg($"Using Dumper Version: {(string.IsNullOrEmpty(dumper.Version) ? "null" : dumper.Version)}");
            MelonLogger.Msg($"Using Il2CppAssemblyUnhollower Version = {(string.IsNullOrEmpty(il2cppassemblyunhollower.Version) ? "null" : il2cppassemblyunhollower.Version)}");
            MelonLogger.Msg($"Using Unity Dependencies Version = {(string.IsNullOrEmpty(unitydependencies.Version) ? "null" : unitydependencies.Version)}");
            if (!string.IsNullOrEmpty(deobfuscationMap.ObfuscationRegex))
                MelonLogger.Msg($"Using Deobfuscation Regex = {deobfuscationMap.ObfuscationRegex}");

            if (!MelonLaunchOptions.Il2CppAssemblyGenerator.OfflineMode)
            {
                if (!dumper.Download()
                    || !il2cppassemblyunhollower.Download()
                    || !unitydependencies.Download()
                    || !deobfuscationMap.Download())
                    return 1;
            }

            // Check for Regex Change against Config

            string CurrentGameAssemblyHash;
            MelonLogger.Msg("Checking GameAssembly...");
            MelonDebug.Msg($"Last GameAssembly Hash: {Config.Values.GameAssemblyHash}");
            MelonDebug.Msg($"Current GameAssembly Hash: {CurrentGameAssemblyHash = GetGameAssemblyHash()}");

            if (!AssemblyGenerationNeeded
                && (string.IsNullOrEmpty(Config.Values.GameAssemblyHash)
                    || !Config.Values.GameAssemblyHash.Equals(CurrentGameAssemblyHash)))
                AssemblyGenerationNeeded = true;

            if (!AssemblyGenerationNeeded)
            {
                MelonLogger.Msg("Assembly is up to date. No Generation Needed.");
                return 0;
            }
            MelonLogger.Msg("Assembly Generation Needed!");

            dumper.Cleanup();
            il2cppassemblyunhollower.Cleanup();

            if (!dumper.Execute())
            {
                dumper.Cleanup();
                return 1;
            }

            if (!il2cppassemblyunhollower.Execute())
            {
                dumper.Cleanup();
                il2cppassemblyunhollower.Cleanup();
                return 1;
            }

            OldFiles_Cleanup();
            OldFiles_LAM();

            dumper.Cleanup();
            il2cppassemblyunhollower.Cleanup();

            MelonLogger.Msg("Assembly Generation Successful!");
            Config.Values.GameAssemblyHash = CurrentGameAssemblyHash;
            deobfuscationMap.Save();

            return 0;
        }

        private static string GetGameAssemblyHash()
        {
            string returnval = null;
            using (MD5 md5 = MD5.Create())
            using (var stream = File.OpenRead(GameAssemblyPath))
            {
                var hash = md5.ComputeHash(stream);
                returnval = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            return returnval;
        }

        private static void OldFiles_Cleanup()
        {
            if (Config.Values.OldFiles.Count <= 0)
                return;
            for (int i = 0; i < Config.Values.OldFiles.Count; i++)
            {
                string filename = Config.Values.OldFiles[i];
                string filepath = Path.Combine(ManagedPath, filename);
                if (File.Exists(filepath))
                {
                    MelonLogger.Msg("Deleting " + filename);
                    File.Delete(filepath);
                }
            }
            Config.Values.OldFiles.Clear();
        }

        private static void OldFiles_LAM()
        {
            string[] filepathtbl = Directory.GetFiles(il2cppassemblyunhollower.Output);
            for (int i = 0; i < filepathtbl.Length; i++)
            {
                string filepath = filepathtbl[i];
                string filename = Path.GetFileName(filepath);
                MelonLogger.Msg("Moving " + filename);
                Config.Values.OldFiles.Add(filename);
                string newfilepath = Path.Combine(ManagedPath, filename);
                if (File.Exists(newfilepath))
                    File.Delete(newfilepath);
                File.Move(filepath, newfilepath);
            }
            Config.Save();
        }
    }
}