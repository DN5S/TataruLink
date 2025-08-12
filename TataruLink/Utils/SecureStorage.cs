using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TataruLink.Services;

namespace TataruLink.Utils;

public static class SecureStorage
{
    // WARNING: Machine/user-specific entropy prevents cross-user decryption
    private static byte[] GetAdditionalEntropy()
    {
        try
        {
            var pluginInterface = Service.PluginInterface;
            var configDir = pluginInterface.GetPluginConfigDirectory();
            var machineId = Environment.MachineName;
            var userId = Environment.UserName;
            var processId = Environment.ProcessId.ToString();

            var uniqueString = $"TataruLink-{configDir}-{machineId}-{userId}-{processId}-APIKey";
            
            var byteCount = Encoding.UTF8.GetByteCount(uniqueString);
            var buffer = ArrayPool<byte>.Shared.Rent(byteCount);
            try
            {
                var actualLength = Encoding.UTF8.GetBytes(uniqueString, 0, uniqueString.Length, buffer, 0);
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
            // WARNING: Fallback entropy still better than hardcoded
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
    
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
            
        try
        {
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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Service.PluginLog.Debug("DPAPI decryption skipped on non-Windows platform.");
                return encryptedText;
            }
            
            byte[] encryptedBytes;
            try
            {
                encryptedBytes = Convert.FromBase64String(encryptedText);
            }
            catch (FormatException)
            {
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
