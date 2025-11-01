using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Yueby.Utils;
using Object = UnityEngine.Object;

namespace Yueby.AvatarTools.Other
{
    public class SyncAnimationParameter : EditorWindow
    {
        public enum ApplyType
        {
            Override,
            Additive
        }

        public enum SyncType
        {
            Animation,
            Mesh
        }

        private SyncType _syncType = SyncType.Animation;
        private ApplyType _applyType = ApplyType.Override;

        private static SyncAnimationParameter _window;
        private AnimationClip _animationClip;

        private bool _isPreview;

        // private readonly Dictionary<string, float> _clipPropertiesDic = new Dictionary<string, float>();
        // private readonly Dictionary<string, float> _meshBlendShapesDefaultDic = new Dictionary<string, float>();

        private SkinnedMeshRenderer _applyMeshRenderer;
        private SkinnedMeshRenderer _dataMeshRenderer;
        private Vector2 _pos, _pos1;

        private AnimationBlendShapeHelper _clipAnimationBsHelper;
        private AnimationBlendShapeHelper _defaultAnimationBsHelper;

        private SerializedObject _clipBsInfoSo;
        private SerializedProperty _clipBsParameters;

        private YuebyReorderableList _bsRL;

        [MenuItem("Tools/YuebyTools/VRChat/Avatar/Sync BlendShapes", false, 40)]
        public static void OpenWindow()
        {
            _window = GetWindow<SyncAnimationParameter>();
        }

        private void OnDisable()
        {
            if (_isPreview)
            {
                _isPreview = false;
                ResetToDefault();
            }

            DestroyImmediate(_clipAnimationBsHelper);
            DestroyImmediate(_defaultAnimationBsHelper);
        }

        private void OnEnable()
        {
            _clipAnimationBsHelper = CreateInstance<AnimationBlendShapeHelper>();
            _defaultAnimationBsHelper = CreateInstance<AnimationBlendShapeHelper>();

        }

        private float OnBsRLDraw(Rect rect, int index, bool isActive, bool isFocused)
        {
            var parameter = _clipAnimationBsHelper.Parameters[index];
            var labelRect = new Rect(rect.x, rect.y, rect.width / 2, rect.height);
            EditorGUI.LabelField(labelRect, parameter.Name);

            var sliderRect = new Rect(labelRect.x + labelRect.width + 5f, labelRect.y, rect.width - 5f - labelRect.width, labelRect.height);
            parameter.Value = EditorGUI.Slider(sliderRect, parameter.Value, 0f, 100f);
            return EditorGUIUtility.singleLineHeight;
        }

        private void OnBsRLRemove(ReorderableList list, Object obj)
        {
            // OnRemove

            Debug.Log(list.count);
        }

        private void OnGUI()
        {
            _clipBsInfoSo?.Update();

            EditorUI.DrawEditorTitle("形态键同步");
            EditorUI.VerticalEGLTitled("参数", () =>
            {
                EditorGUI.BeginChangeCheck();
                _syncType = (SyncType)EditorGUILayout.EnumPopup(_syncType);
                if (EditorGUI.EndChangeCheck())
                {
                    Debug.Log(_syncType);
                }

                EditorGUI.BeginChangeCheck();
                switch (_syncType)
                {
                    case SyncType.Animation:
                        _animationClip = (AnimationClip)EditorUI.ObjectField("动画片段：", 50, _animationClip, typeof(AnimationClip), true);
                        break;
                    case SyncType.Mesh:
                        _dataMeshRenderer = (SkinnedMeshRenderer)EditorUI.ObjectField("数据网格：", 50, _dataMeshRenderer, typeof(SkinnedMeshRenderer), true);
                        break;
                }

                if (EditorGUI.EndChangeCheck())
                    GetAnimationInfo();

                if (_clipAnimationBsHelper == null || _clipAnimationBsHelper.Parameters.Count == 0 && _animationClip != null)
                    GetAnimationInfo();

                EditorGUI.BeginChangeCheck();
                _applyMeshRenderer = (SkinnedMeshRenderer)EditorUI.ObjectField("应用网格：", 50, _applyMeshRenderer, typeof(SkinnedMeshRenderer), true);
                if (EditorGUI.EndChangeCheck())
                    GetMeshInfo();
                if (_defaultAnimationBsHelper == null || _defaultAnimationBsHelper.Parameters.Count == 0 && _applyMeshRenderer != null)
                    GetMeshInfo();
            });

            if (_animationClip != null || _dataMeshRenderer != null)
            {
                _bsRL.DoLayout("动画片段内参数", new Vector2(-1, 400));
            }
            else
                EditorGUILayout.LabelField("请选择你想要同步的对象!");

            EditorUI.VerticalEGLTitled("设置", () =>
            {
                EditorGUI.BeginChangeCheck();
                _applyType = (ApplyType)EditorGUILayout.EnumPopup(_applyType);
                if (EditorGUI.EndChangeCheck())
                {
                    Debug.Log(_applyType);
                }

                EditorUI.HorizontalEGL(() =>
                {
                    var bkgColor = UnityEngine.GUI.backgroundColor;
                    if (_isPreview)
                        UnityEngine.GUI.backgroundColor = Color.green;

                    if (GUILayout.Button("预览"))
                    {
                        _isPreview = !_isPreview;
                        if (!_isPreview)
                        {
                            ResetToDefault();
                        }
                        else
                            ApplyToMesh();

                    }

                    UnityEngine.GUI.backgroundColor = bkgColor;

                    if (GUILayout.Button("应用"))
                    {
                        _isPreview = false;

                        ApplyToMesh();

                        ShowNotification(new GUIContent("已应用形态键！CTRL+Z撤销"));
                    }
                });
            });

            _clipBsInfoSo?.ApplyModifiedProperties();
        }

        private void GetAnimationInfo()
        {

            switch (_syncType)
            {
                case SyncType.Animation:
                    if (_animationClip == null) return;
                    // _clipPropertiesDic.Clear();

                    _clipAnimationBsHelper.Parameters.Clear();
                    var curveBindings = AnimationUtility.GetCurveBindings(_animationClip);
                    foreach (var curveBinding in curveBindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(_animationClip, curveBinding);

                        if (!curveBinding.propertyName.Contains("blendShape.")) continue;
                        _clipAnimationBsHelper.Parameters.Add(new AnimationBlendShapeHelper.Parameter
                        {
                            Name = curveBinding.propertyName.Replace("blendShape.", ""),
                            Value = curve[0].value
                        });
                        // _clipPropertiesDic.Add(curveBinding.propertyName, curve[0].value);
                    }
                    break;
                case SyncType.Mesh:
                    if (_dataMeshRenderer == null) return;

                    _clipAnimationBsHelper.Parameters.Clear();
                    for (var i = 0; i < _dataMeshRenderer.sharedMesh.blendShapeCount; i++)
                    {
                        _clipAnimationBsHelper.Parameters.Add(new AnimationBlendShapeHelper.Parameter
                        {
                            Name = _dataMeshRenderer.sharedMesh.GetBlendShapeName(i),
                            Value = _dataMeshRenderer.GetBlendShapeWeight(i)
                        });
                    }

                    break;
            }

            _clipBsInfoSo = new SerializedObject(_clipAnimationBsHelper);
            _clipBsParameters = _clipBsInfoSo.FindProperty(nameof(AnimationBlendShapeHelper.Parameters));
            _bsRL = new YuebyReorderableList(_clipBsInfoSo, _clipBsParameters, false, true, false, Repaint);
            _bsRL.OnRemove += OnBsRLRemove;
            _bsRL.OnDraw += OnBsRLDraw;
        }

        private void GetMeshInfo()
        {
            if (_applyMeshRenderer == null) return;
            _defaultAnimationBsHelper.Parameters.Clear();

            for (var i = 0; i < _applyMeshRenderer.sharedMesh.blendShapeCount; i++)
            {
                var blendShapeName = _applyMeshRenderer.sharedMesh.GetBlendShapeName(i);
                var value = _applyMeshRenderer.GetBlendShapeWeight(i);

                _defaultAnimationBsHelper.Parameters.Add(new AnimationBlendShapeHelper.Parameter
                {
                    Name = blendShapeName,
                    Value = value
                });

                // _meshBlendShapesDefaultDic.Add(name, value);
            }
        }

        private void ApplyToMesh()
        {
            if (_applyMeshRenderer == null) return;
            if (_animationClip == null && _dataMeshRenderer == null) return;
            Undo.RegisterCompleteObjectUndo(_applyMeshRenderer, "Apply BlendShapes");

            switch (_applyType)
            {
                case ApplyType.Override:
                    foreach (var parameter in _clipAnimationBsHelper.Parameters)
                    {
                        // 检查应用网格中是否存在该形态键
                        var applyIndex = _applyMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                        if (applyIndex >= 0)
                        {
                            _applyMeshRenderer.SetBlendShapeWeight(applyIndex, parameter.Value);
                        }
                        else
                        {
                            Debug.LogWarning($"应用网格中未找到形态键: {parameter.Name}");
                        }
                    }
                    break;
                case ApplyType.Additive:
                    foreach (var parameter in _clipAnimationBsHelper.Parameters)
                    {
                        // 检查应用网格中是否存在该形态键
                        var applyIndex = _applyMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                        if (applyIndex >= 0)
                        {
                            var defaultParam = _defaultAnimationBsHelper.Parameters.FirstOrDefault(x => x.Name == parameter.Name);
                            _applyMeshRenderer.SetBlendShapeWeight(applyIndex, defaultParam?.Value + parameter.Value ?? parameter.Value);
                        }
                        else
                        {
                            Debug.LogWarning($"应用网格中未找到形态键: {parameter.Name}");
                        }
                    }
                    break;
            }
        }

        private void ResetToDefault()
        {
            if (_applyMeshRenderer == null || _animationClip == null) return;

            foreach (var parameter in _defaultAnimationBsHelper.Parameters)
            {
                var index = _applyMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                if (index >= 0)
                {
                    _applyMeshRenderer.SetBlendShapeWeight(index, parameter.Value);
                }
            }
        }
    }

    [Serializable]
    public class AnimationBlendShapeHelper : ScriptableObject
    {
        public List<Parameter> Parameters = new List<Parameter>();

        [Serializable]
        public class Parameter
        {
            public string Name;
            public float Value;
        }
    }
}