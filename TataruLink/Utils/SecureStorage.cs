using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TataruLink.Services;

namespace TataruLink.Utils;

/// <summary>
/// Provides secure storage for sensitive data using Windows Data Protection API (DPAPI)
/// </summary>
public static class SecureStorage
{
    private static readonly byte[] AdditionalEntropy =
    [
        0x54, 0x61, 0x74, 0x61, 0x72, 0x75, 0x4C, 0x69, 0x6E, 0x6B,  // "TataruLink"
        0x41, 0x50, 0x49, 0x4B, 0x65, 0x79                           // "APIKey"
    ];
    
    /// <summary>
    /// Encrypts a string using DPAPI with the current user scope
    /// </summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
            
        try
        {
            // Only available on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Service.PluginLog.Warning("DPAPI encryption is only available on Windows. Storing API key in plain text.");
                return plainText;
            }
            
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes, 
                AdditionalEntropy, 
                DataProtectionScope.CurrentUser
            );
            
            // Convert to Base64 for storage
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to encrypt data. Storing in plain text.");
            return plainText;
        }
    }

    public static string? Unprotect(string? encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;
            
        try
        {
            // Only available on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Service.PluginLog.Debug("DPAPI decryption skipped on non-Windows platform.");
                return encryptedText;
            }
            
            // Try to decode from Base64
            byte[] encryptedBytes;
            try
            {
                encryptedBytes = Convert.FromBase64String(encryptedText);
            }
            catch (FormatException)
            {
                // Not Base64 encoded - probably plain text from an old version
                Service.PluginLog.Debug("API key appears to be unencrypted (legacy format).");
                return encryptedText;
            }
            
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                AdditionalEntropy, 
                DataProtectionScope.CurrentUser
            );
            
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            Service.PluginLog.Error(ex, "Failed to decrypt data. The data may be corrupted or from a different user account.");
            return null;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Unexpected error during decryption.");
            return encryptedText;
        }
    }
    
    public static bool IsProtected(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
            
        try
        {
            _ = Convert.FromBase64String(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
