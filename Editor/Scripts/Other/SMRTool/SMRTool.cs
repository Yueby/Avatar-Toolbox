using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Yueby.Core.Utils;
using Yueby.Utils;

namespace Yueby.Tools
{
    public class SMRTool
    {
        [MenuItem("CONTEXT/SkinnedMeshRenderer/YuebyTools/Set Bounds To One")]
        public static void SetBoundsToOne()
        {
            foreach (var obj in Selection.gameObjects)
            {
                if (!obj.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    YuebyLogger.LogError("No SkinnedMeshRenderer component found");
                    continue;
                }

                Undo.RecordObject(smr, "Set Bounds To One");

                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            }

        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/YuebyTools/Set BlendShapes To Zero")]
        public static void ResetToZero()
        {
            foreach (var obj in Selection.gameObjects)
            {
                if (!obj.TryGetComponent<SkinnedMeshRenderer>(out var smr))
                {
                    YuebyLogger.LogError("No SkinnedMeshRenderer component found");
                    continue;
                }

                Undo.RecordObject(smr, "Reset BlendShape Values");
                var shapeCount = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < shapeCount; i++)
                {
                    smr.SetBlendShapeWeight(i, 0);
                }
            }
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/YuebyTools/Create Animation Clip")]
        public static void CreateAnimationClip()
        {
            if (!Selection.activeGameObject.TryGetComponent<SkinnedMeshRenderer>(out var smr))
            {
                YuebyLogger.LogError("No SkinnedMeshRenderer component found");
                return;
            }

            var validBlendShapesMap = new Dictionary<string, float>();
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                var blendShapeWeight = smr.GetBlendShapeWeight(i);
                if (blendShapeWeight > 0)
                {
                    validBlendShapesMap.Add(smr.sharedMesh.GetBlendShapeName(i), blendShapeWeight);
                }
            }

            if (validBlendShapesMap.Count == 0)
            {
                YuebyLogger.LogWarning("No valid blend shapes found");
                return;
            }

            // open dialog to select file path
            var filePath = EditorUtility.SaveFilePanel("Create Animation Clip", Application.dataPath, smr.name + " BlendShapes", "anim");
            if (string.IsNullOrEmpty(filePath))
            {
                YuebyLogger.LogWarning("No file path selected");
                return;
            }

            // make sure the file path directory exists
            var directory = Path.GetDirectoryName(filePath);

            if (!Directory.Exists(directory))
            {
                // create the directory
                Directory.CreateDirectory(directory);
            }

            // create the animation clip
            var clip = new AnimationClip();
            foreach (var blendShape in validBlendShapesMap)
            {

                var curve = new AnimationCurve
                {
                    keys = new[] { new Keyframe { time = 0, value = blendShape.Value } }
                };
                var bind = new EditorCurveBinding
                {
                    path = smr.name,
                    propertyName = "blendShape." + blendShape.Key,
                    type = typeof(SkinnedMeshRenderer)
                };
                AnimationUtility.SetEditorCurve(clip, bind, curve);
            }
            
            var relativePath = FileUtil.GetProjectRelativePath(filePath);

            // create the animation clip as asset
            AssetDatabase.CreateAsset(clip, relativePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ping object
            EditorUtils.PingProject(clip);
        }

        [MenuItem("CONTEXT/SkinnedMeshRenderer/YuebyTools/Create Animation Clip", validate = true)]
        public static bool CreateAnimationClipValidate()
        {
            // check if multiple objects are selected
            if (Selection.gameObjects.Length > 1)
            {
                return false;
            }

            return true;
        }
    }
}