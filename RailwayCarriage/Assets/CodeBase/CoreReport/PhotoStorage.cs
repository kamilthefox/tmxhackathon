using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class PhotoStorage
{
    public static byte[] LastPhotoBytes { get; private set; }
    public static Texture2D LastPhotoTexture { get; private set; }
    public static DateTime LastPhotoTime { get; private set; }

    public static void StorePhoto(byte[] bytes, Texture2D texture)
    {
        LastPhotoBytes = bytes;
        LastPhotoTexture = texture;
        LastPhotoTime = DateTime.Now;
    }

    public static void Clear()
    {
        LastPhotoBytes = null;
        if (LastPhotoTexture != null)
        {
            UnityEngine.Object.Destroy(LastPhotoTexture);
            LastPhotoTexture = null;
        }
    }
}