using System.IO;
using UnityEngine;

/// <summary>
/// Espacio de almacenamiento del dispositivo. En Android usa StatFs sobre la ruta
/// de datos; en editor/standalone usa DriveInfo. Si no se puede medir, total = 0.
/// </summary>
public static class DeviceStorage
{
    /// <summary>(usadoBytes, totalBytes). total = 0 si no se pudo medir.</summary>
    public static (long used, long total) Get()
    {
        long free = 0, total = 0;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var statFs = new AndroidJavaObject("android.os.StatFs", Application.persistentDataPath))
            {
                long blockSize   = statFs.Call<long>("getBlockSizeLong");
                long totalBlocks = statFs.Call<long>("getBlockCountLong");
                long availBlocks = statFs.Call<long>("getAvailableBlocksLong");
                total = blockSize * totalBlocks;
                free  = blockSize * availBlocks;
            }
        }
        catch (System.Exception e) { Debug.LogWarning($"[DeviceStorage] StatFs falló: {e.Message}"); }
#else
        try
        {
            var root  = Path.GetPathRoot(Application.persistentDataPath);
            var drive = new DriveInfo(root);
            total = drive.TotalSize;
            free  = drive.AvailableFreeSpace;
        }
        catch (System.Exception e) { Debug.LogWarning($"[DeviceStorage] DriveInfo falló: {e.Message}"); }
#endif

        long used = total - free;
        if (used < 0) used = 0;
        return (used, total);
    }

    public static float ToGB(long bytes) => bytes / (1024f * 1024f * 1024f);
}
