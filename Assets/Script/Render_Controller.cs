using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.IO;
using System.Collections;

namespace Softdrink{

	[System.Serializable]
	public class AdvancedOutputSettings
	{	
		public bool useAdvancedRender = false;
		public int width = 1920;
		public int height = 1080;
		public Canvas previewCanvas = null;
		public RawImage previewImage = null;
		[TooltipAttribute("Should Gamma Correction be applied to the output images?\nThis is often necessary when working in Linear space!")]
		public bool applyGammaCorrection = true;
	}

	public class Render_Controller : MonoBehaviour {

		[TooltipAttribute("Should an image sequence be rendered?")]
		public bool render = false;

		[TooltipAttribute("The playback framerate.")]
		public float playbackFramerate = 30f;

		[TooltipAttribute("The data path to the folder to save frames.")]
		public string renderDirectory = "./Render/";

		[TooltipAttribute("The Base Name to use (before appending frame number).")]
		public string baseName = "JPL_Trajectory_Vis_";

		[TooltipAttribute("The frame on which to start the render. \nA value of 0 is the first frame.")]
		public int startFrame = 0;

		[TooltipAttribute("The number of frames to render. \nA value of 0 is used for unlimited.")]
		public int desiredFrames = 100;

		[TooltipAttribute("Exit Play after the render finishes?")]
		public bool exitOnRenderCompletion = true;

		[TooltipAttribute("Show detailed render info (elapsed and estimated time) in the console.")]
		public bool useDetailedInfo = true;

		[TooltipAttribute("READONLY: The number of frames that have been rendered.")]
		public int renderedFrames = 0;

		public AdvancedOutputSettings advancedSettings;
		private RenderTexture targetTexture = null;
		private Texture2D frameBuffer = null;

		private float elapsedTime = 0f;
		private float percentageComplete = 0f;
		private float estimatedTime = 0f;
		private float ratio = 0f;

		private bool hasShownRenderCompleteMessage = false;

		void OnValidate()
		{
			if(startFrame < 0) startFrame = 0;
		}

		void Awake(){
			if(render){
				// Time.captureFramerate = playbackFramerate;
				Time.captureDeltaTime = 1.0f/playbackFramerate;

				System.IO.Directory.CreateDirectory(renderDirectory);
				Debug.Log("Created or found render directory...", this);
			}
			renderedFrames = 0;

			if(advancedSettings.useAdvancedRender)
			{
				if(advancedSettings.previewCanvas == null) Debug.LogError("ERROR: No Preview Canvas found. Render will not be previewable.", this);
				if(advancedSettings.previewImage == null) Debug.LogError("ERROR: No Preview Image found. Render will not be previewable.", this);
				targetTexture = new RenderTexture(
						advancedSettings.width,
						advancedSettings.height,
						0,
						RenderTextureFormat.ARGB32,
						RenderTextureReadWrite.Default
					);
				frameBuffer = new Texture2D(
						advancedSettings.width,
						advancedSettings.height,
						TextureFormat.ARGB32, 
						false,
						false
					);
				Camera.main.targetTexture = targetTexture;
				if(advancedSettings.previewImage != null)
				{
					advancedSettings.previewImage.texture = targetTexture;
					advancedSettings.previewImage.color = Color.white;
				}
			}
		}

		void Start()
		{
			if(render && startFrame <= 0) StartCoroutine(renderRoutine());
		}
		
		void Update () {
			if(!render) return;

			if(Time.frameCount < startFrame) return;

			if(desiredFrames > 0){
				if(renderedFrames > desiredFrames){
					render = false;
					if(!hasShownRenderCompleteMessage){
						Debug.Log("Render completed! \n" + elapsedTime.ToString("F2") + "s elapsed.", this);
						if(exitOnRenderCompletion){
							#if UNITY_EDITOR
								UnityEditor.EditorApplication.isPlaying = false;
							#endif
							Application.Quit();
						}
						hasShownRenderCompleteMessage = true;
					}
				}
			}
			
		 	if(render) StartCoroutine(renderRoutine());
		}

		[ContextMenu("Choose Directory")]
		public void ChooseDirectory(){
			string testDir = Application.dataPath;
			if(!String.IsNullOrEmpty(renderDirectory) && Directory.Exists(renderDirectory)) testDir = renderDirectory;
	    	string dir = EditorUtility.OpenFolderPanel("Choose Render Output Directory", Application.dataPath, "");
	    	if(String.IsNullOrEmpty(dir)) return;
	    	if(!Directory.Exists(dir)) return;
	    	renderDirectory = dir;
		}

		void CalcProgress(){
			percentageComplete = (float)renderedFrames / (float)desiredFrames;
			elapsedTime = Time.unscaledTime;

			if(desiredFrames != 0) ratio = percentageComplete/elapsedTime;

			if(desiredFrames != 0) estimatedTime = (1.0f - percentageComplete)/ratio;
		}

		IEnumerator renderRoutine(){
			if(!render) yield break;
			yield return new WaitForEndOfFrame();


			 // Append filename to folder name (format is '0005 shot.png"')
	        string name = string.Format("{0}/" + baseName + "{1:D04}.png", renderDirectory, renderedFrames);

	        CalcProgress();

	        if(Time.frameCount % 10 == 1){
	        	string info = "Rendered frame " + renderedFrames;
	        	if(desiredFrames != 0) info += "of " + desiredFrames;
	        	if(useDetailedInfo){
	        		info += "\nRender is " + (percentageComplete*100f).ToString("F2") + "% complete";
	        		info += "\t" + elapsedTime.ToString("F2") + "s Elapsed; ";
	        		if(desiredFrames != 0) info += estimatedTime.ToString("F2") + "s Estimated remain";
	        	}
			    Debug.Log(info, this);
			}
	        
		    // Capture the screenshot to the specified file.
	        if(!advancedSettings.useAdvancedRender) ScreenCapture.CaptureScreenshot(name);
	        else
	        {
	        	RenderTexture pActive = RenderTexture.active;
	        	RenderTexture.active = targetTexture;
	        	frameBuffer.ReadPixels(new Rect(0,0,advancedSettings.width,advancedSettings.height), 0,0);
	        	// Gamma correction
		    	if(advancedSettings.applyGammaCorrection)
		    	{
			    	Color[] pixels = frameBuffer.GetPixels();
			        for (int p = 0; p < pixels.Length; p++)
			        {
			            pixels[p] = pixels[p].gamma;
			        }
			        frameBuffer.SetPixels(pixels);
			    }
	        	byte[] raw = frameBuffer.EncodeToPNG();
	        	System.IO.File.WriteAllBytes(name, raw);
	        	RenderTexture.active = pActive;
	        }

	        renderedFrames++;
		}
	}

}
