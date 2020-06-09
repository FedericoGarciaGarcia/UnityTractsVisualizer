﻿
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ScreenTextureCapturer : MonoBehaviour
    {
        [SerializeField] private int _screenshotTextureW = 1280, _screenshotTextureH = 720;
 
        public Texture2D ScreenshotTexture { get; private set; }
       
        private void Awake()
        {  
            ScreenshotTexture = new Texture2D(_screenshotTextureW, _screenshotTextureH, TextureFormat.RGB24, false);
        }
 
        public void UpdateScreenshotTexture(bool grayscale)
        {
            RenderTexture transformedRenderTexture = null;
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                Screen.width,
                Screen.height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default,
                1);
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);
                transformedRenderTexture = RenderTexture.GetTemporary(
                    ScreenshotTexture.width,
                    ScreenshotTexture.height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default,
                    1);
                Graphics.Blit(
                    renderTexture,
                    transformedRenderTexture,
                    new Vector2(1.0f, -1.0f),
                    new Vector2(0.0f, 1.0f));
                RenderTexture.active = transformedRenderTexture;
                ScreenshotTexture.ReadPixels(
                    new Rect(0, 0, ScreenshotTexture.width, ScreenshotTexture.height),
                    0, 0);
            }
            catch (Exception e)
            {
                Debug.Log("Exception: " + e);
            }
            finally
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (transformedRenderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(transformedRenderTexture);
                }
            }
 
            ScreenshotTexture.Apply();
        }
		
		void Update()
		{
			if (Input.GetKeyDown("space"))
			{
				UpdateScreenshotTexture(false);
				byte[] _bytes = ScreenshotTexture.EncodeToPNG();
				System.IO.File.WriteAllBytes("C:\\Users\\FEDE\\Desktop\\screenshot.png", _bytes);
				Debug.Log("Screenshot taken");
			}
		}
    }