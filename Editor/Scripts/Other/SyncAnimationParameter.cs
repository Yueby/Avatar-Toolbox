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

        private SkinnedMeshRenderer _targetMeshRenderer;
        private SkinnedMeshRenderer _sourceMeshRenderer;
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
                        _sourceMeshRenderer = (SkinnedMeshRenderer)EditorUI.ObjectField("源网格：", 50, _sourceMeshRenderer, typeof(SkinnedMeshRenderer), true);
                        break;
                }

                if (EditorGUI.EndChangeCheck())
                    GetAnimationInfo();

                if (_clipAnimationBsHelper == null || _clipAnimationBsHelper.Parameters.Count == 0 && _animationClip != null)
                    GetAnimationInfo();

                EditorGUI.BeginChangeCheck();
                _targetMeshRenderer = (SkinnedMeshRenderer)EditorUI.ObjectField("目标网格：", 50, _targetMeshRenderer, typeof(SkinnedMeshRenderer), true);
                if (EditorGUI.EndChangeCheck())
                    GetMeshInfo();
                if (_defaultAnimationBsHelper == null || _defaultAnimationBsHelper.Parameters.Count == 0 && _targetMeshRenderer != null)
                    GetMeshInfo();
            });

            if (_animationClip != null || _sourceMeshRenderer != null)
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
                    if (_sourceMeshRenderer == null) return;

                    _clipAnimationBsHelper.Parameters.Clear();
                    for (var i = 0; i < _sourceMeshRenderer.sharedMesh.blendShapeCount; i++)
                    {
                        _clipAnimationBsHelper.Parameters.Add(new AnimationBlendShapeHelper.Parameter
                        {
                            Name = _sourceMeshRenderer.sharedMesh.GetBlendShapeName(i),
                            Value = _sourceMeshRenderer.GetBlendShapeWeight(i)
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
            if (_targetMeshRenderer == null) return;
            _defaultAnimationBsHelper.Parameters.Clear();

            for (var i = 0; i < _targetMeshRenderer.sharedMesh.blendShapeCount; i++)
            {
                var blendShapeName = _targetMeshRenderer.sharedMesh.GetBlendShapeName(i);
                var value = _targetMeshRenderer.GetBlendShapeWeight(i);

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
            if (_targetMeshRenderer == null) return;
            if (_animationClip == null && _sourceMeshRenderer == null) return;
            Undo.RegisterCompleteObjectUndo(_targetMeshRenderer, "Apply BlendShapes");
            switch (_applyType)
            {
                case ApplyType.Override:
                    // for (var i = 0; i < _meshRenderer.sharedMesh.blendShapeCount; i++)
                    // {
                    //     var blendShapeName = _meshRenderer.sharedMesh.GetBlendShapeName(i);
                    //     var index = _meshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);

                    //     _meshRenderer.SetBlendShapeWeight(index, 0);
                    // }

                    foreach (var parameter in _clipAnimationBsHelper.Parameters)
                    {
                        var index = _targetMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                        _targetMeshRenderer.SetBlendShapeWeight(index, parameter.Value);
                    }
                    break;
                case ApplyType.Additive:
                    foreach (var parameter in _clipAnimationBsHelper.Parameters)
                    {
                        var index = _targetMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                        var defaultParam = _defaultAnimationBsHelper.Parameters.FirstOrDefault(x => x.Name == parameter.Name);
                        _targetMeshRenderer.SetBlendShapeWeight(index, defaultParam.Value + parameter.Value);
                    }
                    break;
            }
        }

        private void ResetToDefault()
        {
            if (_targetMeshRenderer == null || _animationClip == null) return;

            foreach (var parameter in _defaultAnimationBsHelper.Parameters)
            {
                var index = _targetMeshRenderer.sharedMesh.GetBlendShapeIndex(parameter.Name);
                _targetMeshRenderer.SetBlendShapeWeight(index, parameter.Value);
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