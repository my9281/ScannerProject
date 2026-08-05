using Newtonsoft.Json;
using Scanner.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Scanner.Services
{
    public class RememberedLogin
    {
        public bool RememberPassword
        {
            get;
            set;
        }

        public string Username
        {
            get;
            set;
        }

        public string Password
        {
            get;
            set;
        }
    }

    internal class StoredLoginData
    {
        public bool RememberPassword
        {
            get;
            set;
        }

        public string EncryptedUsername
        {
            get;
            set;
        }

        public string EncryptedPassword
        {
            get;
            set;
        }
    }

    public static class LocalAuthStore
    {
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes(
                "Scanner.Login.Storage.v1"
            );

        private static readonly string DataFolder = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string SessionFile =
            Path.Combine(
                DataFolder,
                "session.dat"
            );

        private static readonly string LoginFile =
            Path.Combine(
                DataFolder,
                "login.dat"
            );

        public static void SaveSession(
            AuthSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(
                    "session"
                );
            }

            EnsureDataFolder();

            string json =
                JsonConvert.SerializeObject(
                    session
                );

            string encrypted =
                Encrypt(json);

            File.WriteAllText(
                SessionFile,
                encrypted,
                Encoding.UTF8
            );
        }

        public static AuthSession LoadSession()
        {
            if (!File.Exists(SessionFile))
            {
                return null;
            }

            try
            {
                string encrypted =
                    File.ReadAllText(
                        SessionFile,
                        Encoding.UTF8
                    );

                string json =
                    Decrypt(encrypted);

                AuthSession session =
                    JsonConvert.DeserializeObject
                        <AuthSession>(json);

                if (session == null ||
                    !session.IsValid())
                {
                    ClearSession();

                    return null;
                }

                return session;
            }
            catch
            {
                ClearSession();

                return null;
            }
        }

        public static void ClearSession()
        {
            TryDeleteFile(SessionFile);
        }

        public static void SaveRememberedLogin(
            string username,
            string password,
            bool rememberPassword)
        {
            EnsureDataFolder();

            if (!rememberPassword)
            {
                ClearRememberedLogin();

                return;
            }

            StoredLoginData data =
                new StoredLoginData();

            data.RememberPassword = true;

            data.EncryptedUsername =
                Encrypt(
                    username ?? string.Empty
                );

            data.EncryptedPassword =
                Encrypt(
                    password ?? string.Empty
                );

            string json =
                JsonConvert.SerializeObject(
                    data
                );

            File.WriteAllText(
                LoginFile,
                json,
                Encoding.UTF8
            );
        }

        public static RememberedLogin
            LoadRememberedLogin()
        {
            if (!File.Exists(LoginFile))
            {
                return null;
            }

            try
            {
                string json =
                    File.ReadAllText(
                        LoginFile,
                        Encoding.UTF8
                    );

                StoredLoginData data =
                    JsonConvert.DeserializeObject
                        <StoredLoginData>(json);

                if (data == null ||
                    !data.RememberPassword)
                {
                    return null;
                }

                RememberedLogin login =
                    new RememberedLogin();

                login.RememberPassword = true;

                login.Username =
                    Decrypt(
                        data.EncryptedUsername
                    );

                login.Password =
                    Decrypt(
                        data.EncryptedPassword
                    );

                return login;
            }
            catch
            {
                ClearRememberedLogin();

                return null;
            }
        }

        public static void ClearRememberedLogin()
        {
            TryDeleteFile(LoginFile);
        }

        private static string Encrypt(
            string plainText)
        {
            byte[] plainBytes =
                Encoding.UTF8.GetBytes(
                    plainText ?? string.Empty
                );

            byte[] encryptedBytes =
                ProtectedData.Protect(
                    plainBytes,
                    Entropy,
                    DataProtectionScope
                        .CurrentUser
                );

            return Convert.ToBase64String(
                encryptedBytes
            );
        }

        private static string Decrypt(
            string encryptedText)
        {
            if (string.IsNullOrWhiteSpace(
                    encryptedText
                ))
            {
                return string.Empty;
            }

            byte[] encryptedBytes =
                Convert.FromBase64String(
                    encryptedText
                );

            byte[] plainBytes =
                ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope
                        .CurrentUser
                );

            return Encoding.UTF8.GetString(
                plainBytes
            );
        }

        private static void EnsureDataFolder()
        {
            Directory.CreateDirectory(
                DataFolder
            );
        }

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 清理失败不影响主程序。
            }
        }
    }
}