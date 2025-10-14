using System;
using System.Security.Cryptography;
using System.Text;
using TataruLink.Services;

namespace TataruLink.Utils;

/// <summary>
/// Simple cross-platform encryption for API keys using AES-256-GCM.
/// Uses machine/user-specific data to derive encryption key.
/// </summary>
public static class SecureStorage
{
    private const string EncryptedPrefix = "enc_v1:";
    private const int NonceSize = 12; // 96 bits for AES-GCM
    private const int TagSize = 16;   // 128 bits authentication tag

    /// <summary>
    /// Derives a 256-bit encryption key from machine/user-specific data.
    /// </summary>
    private static byte[] DeriveKey()
    {
        try
        {
            var pluginInterface = Service.PluginInterface;
            var configDir = pluginInterface.GetPluginConfigDirectory();
            var machineId = Environment.MachineName;
            var userId = Environment.UserName;

            // Combine machine and user-specific data
            var keyMaterial = $"TataruLink-{configDir}-{machineId}-{userId}";
            var keyBytes = Encoding.UTF8.GetBytes(keyMaterial);

            // Use SHA256 to derive a consistent 256-bit key
            return SHA256.HashData(keyBytes);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Warning(ex, "Failed to generate encryption key, using fallback");

            // Fallback: less secure but still better than plaintext
            var fallback = $"TataruLink-{Environment.MachineName}-{Environment.UserName}";
            var fallbackBytes = Encoding.UTF8.GetBytes(fallback);
            return SHA256.HashData(fallbackBytes);
        }
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM.
    /// </summary>
    public static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var key = DeriveKey();
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];

            // Generate random nonce
            RandomNumberGenerator.Fill(nonce);

            // Encrypt using AES-GCM
            var cipherBytes = new byte[plainBytes.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Format: nonce + tag + ciphertext
            var result = new byte[NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, result, NonceSize + TagSize, cipherBytes.Length);

            // Clear sensitive data
            Array.Clear(plainBytes, 0, plainBytes.Length);
            Array.Clear(key, 0, key.Length);

            return EncryptedPrefix + Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to encrypt data.");
            throw;
        }
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM.
    /// </summary>
    public static string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
            return protectedText;

        // If not encrypted, return as-is (for backward compatibility)
        if (!protectedText.StartsWith(EncryptedPrefix))
        {
            Service.PluginLog.Warning("Data is not encrypted. Returning as-is.");
            return protectedText;
        }

        try
        {
            var encryptedText = protectedText.Substring(EncryptedPrefix.Length);
            var encryptedData = Convert.FromBase64String(encryptedText);

            // Validate minimum length: nonce + tag + at least 1 byte
            if (encryptedData.Length < NonceSize + TagSize + 1)
            {
                Service.PluginLog.Error("Invalid encrypted data format.");
                return null;
            }

            // Extract components
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var cipherBytes = new byte[encryptedData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(encryptedData, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

            // Decrypt using AES-GCM
            var key = DeriveKey();
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            // Clear sensitive data
            Array.Clear(key, 0, key.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            Service.PluginLog.Error(ex, "Failed to decrypt data. The data may be corrupted or from a different machine/user.");
            return null;
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Unexpected error during decryption.");
            return null;
        }
    }
}
