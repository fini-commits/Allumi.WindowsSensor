using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Allumi.WindowsSensor.Auth
{
    /// <summary>
    /// Manages secure storage of API keys using Windows Credential Manager
    /// </summary>
    public static class CredentialManager
    {
        private const string TARGET_NAME = "Allumi.WindowsSensor.ApiKey";

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, int reservedFlag);

        [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        private static extern void CredFree([In] IntPtr cred);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        /// <summary>
        /// Securely stores the API key in Windows Credential Manager
        /// </summary>
        public static bool SaveApiKey(string apiKey)
        {
            try
            {
                byte[] byteArray = Encoding.Unicode.GetBytes(apiKey);
                IntPtr credPtr = Marshal.AllocCoTaskMem(byteArray.Length);
                Marshal.Copy(byteArray, 0, credPtr, byteArray.Length);

                CREDENTIAL credential = new CREDENTIAL
                {
                    Type = 1, // CRED_TYPE_GENERIC
                    TargetName = TARGET_NAME,
                    CredentialBlob = credPtr,
                    CredentialBlobSize = (uint)byteArray.Length,
                    Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
                    UserName = Environment.UserName,
                    Comment = "Allumi Windows Sensor API Key"
                };

                bool result = CredWrite(ref credential, 0);
                Marshal.FreeCoTaskMem(credPtr);
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save API key: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves the API key from Windows Credential Manager
        /// </summary>
        public static string? GetApiKey()
        {
            try
            {
                bool result = CredRead(TARGET_NAME, 1, 0, out IntPtr credPtr);
                
                if (!result)
                    return null;

                CREDENTIAL credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                
                byte[] byteArray = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, byteArray, 0, (int)credential.CredentialBlobSize);
                
                CredFree(credPtr);
                
                return Encoding.Unicode.GetString(byteArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve API key: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Removes the API key from Windows Credential Manager
        /// </summary>
        public static bool DeleteApiKey()
        {
            try
            {
                return CredDelete(TARGET_NAME, 1, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete API key: {ex.Message}");
                return false;
            }
        }
    }
}
