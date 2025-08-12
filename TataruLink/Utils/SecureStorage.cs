using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TataruLink.Services;

namespace TataruLink.Utils;

/// <summary>
/// Provides secure storage for sensitive data using Windows Data Protection API (DPAPI)
/// with improved entropy generation for better security.
/// </summary>
public static class SecureStorage
{
    /// <summary>
    /// Generate user and machine-specific entropy for stronger encryption.
    /// This prevents other processes from decrypting the data even with decompiled code.
    /// </summary>
    private static byte[] GetAdditionalEntropy()
    {
        try
        {
            // Combine multiple unique identifiers for stronger entropy
            var pluginInterface = Service.PluginInterface;
            var configDir = pluginInterface.GetPluginConfigDirectory();
            var machineId = Environment.MachineName;
            var userId = Environment.UserName;
            var processId = Environment.ProcessId.ToString();

            // Create a unique string from multiple sources
            var uniqueString = $"TataruLink-{configDir}-{machineId}-{userId}-{processId}-APIKey";
            
            // Use ArrayPool for a temporary buffer
            var byteCount = Encoding.UTF8.GetByteCount(uniqueString);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var actualLength = Encoding.UTF8.GetBytes(uniqueString, 0, uniqueString.Length, buffer, 0);
                // Generate SHA256 hash as entropy (32 bytes)
                return SHA256.HashData(buffer.AsSpan(0, actualLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, "Failed to generate unique entropy, using fallback");
            // Fallback to basic entropy if something goes wrong
            // This is still better than hardcoded values
            var fallback = $"TataruLink-{Environment.MachineName}-{DateTime.UtcNow.Year}";
            
            var byteCount = Encoding.UTF8.GetByteCount(fallback);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var actualLength = Encoding.UTF8.GetBytes(fallback, 0, fallback.Length, buffer, 0);
                return SHA256.HashData(buffer.AsSpan(0, actualLength));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }
    }
    
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
                throw new PlatformNotSupportedException("DPAPI encryption is only available on Windows.");
            }
            
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var entropy = GetAdditionalEntropy();
            var encryptedBytes = ProtectedData.Protect(
                plainBytes, 
                entropy, 
                DataProtectionScope.CurrentUser
            );
            
            // Convert to Base64 for storage
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to encrypt data.");
            throw;
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
                // Not Base64 encoded - invalid format
                Service.PluginLog.Error("Invalid encrypted data format.");
                return null;
            }
            
            var entropy = GetAdditionalEntropy();
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                entropy, 
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
}
