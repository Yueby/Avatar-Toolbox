using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Yueby.Tools.AvatarToolbox.MaterialPreset;
using Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Components;

namespace Yueby.Tools.AvatarToolbox.MaterialPreset.Editor.Services
{
    public class ThumbnailService
    {
        private MaterialPresetManager _manager;
        private PresetGridView _gridView;
        private PresetObjectFactory _objectFactory;

        public ThumbnailService(MaterialPresetManager manager, PresetGridView gridView, PresetObjectFactory objectFactory)
        {
            _manager = manager;
            _gridView = gridView;
            _objectFactory = objectFactory;
        }

        public void CaptureThumbnailWithDialog(PresetGroup group)
        {
            if (group == null || _manager == null) return;

            GameObject tempClone = null;

            try
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(_manager.gameObject);
                if (prefabAsset != null)
                {
                    tempClone = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                }
                else
                {
                    tempClone = Object.Instantiate(_manager.gameObject);
                }

                if (tempClone == null) return;

                tempClone.transform.position = _manager.transform.position + new Vector3(10000, 10000, 10000);
                tempClone.name = "TempCaptureObject";

                var manager = tempClone.GetComponent<MaterialPresetManager>();
                if (manager != null) manager.enabled = false;

                _objectFactory.ApplyGroupToRoot(group, tempClone.transform);

                CaptureThumbnail(group, tempClone);
            }
            finally
            {
                if (tempClone != null)
                {
                    Object.DestroyImmediate(tempClone);
                }
            }
        }

        private void CaptureThumbnail(PresetGroup group, GameObject targetObj)
        {
            if (group == null || targetObj == null) return;

            var renderers = targetObj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 boundsCenter = bounds.center;
            Vector3 boundsSize = bounds.size;
            float maxSize = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);

            int tempLayer = 31;
            var originalLayers = new Dictionary<GameObject, int>();

            void SetLayerRecursively(Transform trans)
            {
                originalLayers[trans.gameObject] = trans.gameObject.layer;
                trans.gameObject.layer = tempLayer;
                foreach (Transform child in trans)
                {
                    SetLayerRecursively(child);
                }
            }
            SetLayerRecursively(targetObj.transform);

            var camObj = new GameObject("TempCaptureCam");
            var cam = camObj.AddComponent<Camera>();
            cam.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

            cam.cullingMask = 1 << tempLayer;

            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = maxSize * 10f;

            float fovRadians = cam.fieldOfView * Mathf.Deg2Rad;
            float distance = maxSize / (2.0f * Mathf.Tan(fovRadians * 0.5f));

            Vector3 cameraDirection = new Vector3(1f, 0.5f, 1f).normalized;

            cam.transform.position = boundsCenter + cameraDirection * distance * 1.8f;

            cam.transform.LookAt(boundsCenter);

            int res = 256;
            RenderTexture rt = RenderTexture.GetTemporary(res, res, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camObj);

            foreach (var kvp in originalLayers)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.layer = kvp.Value;
                }
            }

            Undo.RecordObject(_manager, "捕获缩略图");
            group.Thumbnail = tex;
            SaveThumbnailAsset(group, tex);
            EditorUtility.SetDirty(_manager);
            _gridView?.Refresh(_manager);
        }

        private void SaveThumbnailAsset(PresetGroup group, Texture2D texture)
        {
            if (group == null || texture == null) return;

            if (group.Thumbnail != null)
            {
                string oldPath = AssetDatabase.GetAssetPath(group.Thumbnail);
                if (!string.IsNullOrEmpty(oldPath))
                {
                    AssetDatabase.DeleteAsset(oldPath);
                    group.Thumbnail = null;
                }
            }

            string folderPath = "Assets/MaterialPresets_Thumbnails";
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            string fileName = $"Thumb_{group.UUID}.png";
            string path = $"{folderPath}/{fileName}";

            if (System.IO.File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();

            Texture2D assetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.GUI;
                importer.SaveAndReimport();
            }
            group.Thumbnail = assetTex;
        }
    }
}

