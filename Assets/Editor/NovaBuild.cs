using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NovaBuild
{
    [MenuItem("Nova/Prepare Story Scene")]
    public static void Prepare()
    {
        Directory.CreateDirectory("Assets/Scenes");
        foreach(var p in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Nova"})) {
            var path=AssetDatabase.GUIDToAssetPath(p);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Default;imp.isReadable=true;imp.alphaIsTransparency=true;imp.maxTextureSize=1024;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;
        new GameObject("Nova Storybook",typeof(NovaStorybook));
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Nova.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Nova.unity",true)};
        PlayerSettings.companyName="NovaStorybook";PlayerSettings.productName="Nova and the Lost Signal";PlayerSettings.defaultScreenWidth=720;PlayerSettings.defaultScreenHeight=1280;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Gamma;PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback=false;PlayerSettings.WebGL.dataCaching=true;
        PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.FullWithoutStacktrace;
        AssetDatabase.SaveAssets();
        Debug.Log("NOVA_PREPARED");
    }
    [MenuItem("Nova/Build Windows")]
    public static void Windows()
    {
        Prepare();Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Nova.unity"},locationPathName="Builds/Windows/Nova.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText("Builds/windows_build_summary.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed");
    }
    [MenuItem("Nova/Build WebGL")]
    public static void Web()
    {
        Prepare();Directory.CreateDirectory("Builds/WebGL");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Nova.unity"},locationPathName="Builds/WebGL",target=BuildTarget.WebGL,options=BuildOptions.None});
        File.WriteAllText("Builds/webgl_build_summary.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL build failed");
    }
}
