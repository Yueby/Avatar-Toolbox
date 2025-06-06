using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Yueby.AvatarTools.Other;
using Yueby.AvatarTools.Other.ModalWindow;
using Yueby.Utils;

namespace Yueby.AvatarTools.DressingTools
{
    public class DressingToolEditorWindow : EditorWindow
    {
        private const string DescriptorPrefs = "Descriptor";
        private static readonly DTLocalization _localization = new DTLocalization();
        private static VRCAvatarDescriptor _descriptor;

        private static string _autoParentKeyword = "Clothes";

        private static DressingToolEditorWindow _window;

        private readonly string[] _poseName =
        {
            "TPose", "IdlePose", "Walk1Pose", "Walk2Pose", "RunPose", "PronePose",
            "Custom1", "Custom2", "Custom3", "Custom4", "Custom5", "Custom6"
        };

        private Animator _avatarAnimator;

        private Transform _avatarArmature, _clothesArmature;
        private GameObject _clothes, _preClothes;
        private bool _hasSameNameClothes;

        private bool _isExtractPb;
        private bool _isAutoParentKeywordObjectRoot;
        private bool _isDuplicateDress = true;

        private bool _isFocusAvatar;

        private bool _isSureDressing;
        private bool _isValidClothes, _isBoneNameWrong;
        private GameObject _preDescriptorGo;

        private RuntimeAnimatorController _runtimeAnimatorController;
        private int _toolBarSelectionIndex;

        private bool _useClothesName = true;
        private readonly Texture2D[] _titleIcons = new Texture2D[2];
        private List<VRCPhysBone> _waitCombinePhysBones;

        private void OnEnable()
        {
            _titleIcons[0] = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/yueby.tools.avatar-toolbox/Editor/Assets/DressingTools/Sprites/icon_d.png");
            _titleIcons[1] = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/yueby.tools.avatar-toolbox/Editor/Assets/DressingTools/Sprites/icon_l.png");

            if (_descriptor == null)
            {
                if (EditorPrefs.HasKey(DescriptorPrefs))
                {
                    _descriptor = (VRCAvatarDescriptor)EditorUtility.InstanceIDToObject(EditorPrefs.GetInt(DescriptorPrefs));
                }
                else
                {
                    VRCAvatarDescriptor selectionDescriptor = null;
                    if (Selection.activeGameObject != null)
                        selectionDescriptor = Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>();

                    if (selectionDescriptor != null)
                    {
                        _descriptor = selectionDescriptor;
                    }
                    else
                    {
                        var descriptors = FindObjectsOfType<VRCAvatarDescriptor>();
                        if (descriptors.Length > 0)
                            _descriptor = descriptors[0];
                    }
                }
            }

            _useClothesName = true;
            _isDuplicateDress = true;
            _hasSameNameClothes = false;
            _useClothesName = true;
            _isFocusAvatar = false;
            _isAutoParentKeywordObjectRoot = true;
            _toolBarSelectionIndex = 0;
            _isExtractPb = false;


            var path = GetControllerFolder();
            _path = string.IsNullOrEmpty(path) ? DefaultControllerPath : path;
            _runtimeAnimatorController = AssetDatabase.LoadMainAssetAtPath($"{_path}/AvatarTest.controller") as RuntimeAnimatorController;
        }

        private const string DefaultControllerPath = "Packages/yueby.tools.avatar-toolbox/Editor/Assets/DressingTools/Animation/AvatarTest";
        private static string _path = DefaultControllerPath;

        private string GetAnimationAssetsPath()
        {
            return _path.Replace("/AvatarTest", "");
        }

        private string GetControllerFolder()
        {
            var assets = AssetDatabase.FindAssets("AvatarTest");
            foreach (var asset in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                if (AssetDatabase.IsValidFolder(path))
                {
                    if (path.StartsWith("Assets") && path.Contains("DressingTools"))
                    {
                        return path;
                    }
                }
            }

            return "";
        }

        private void OnDestroy()
        {
            if (EditorPrefs.HasKey(DescriptorPrefs))
                EditorPrefs.DeleteKey(DescriptorPrefs);
        }

        private void OnGUI()
        {
            if (_window == null)
                _window = GetWindow<DressingToolEditorWindow>();
            _window.titleContent = new GUIContent(_localization.Get("title_main_label"), _titleIcons[!EditorGUIUtility.isProSkin ? 1 : 0]);
            EditorUI.DrawEditorTitle(_localization.Get("title_main_label"));

            _localization.DrawLanguageUI();

            if (EditorApplication.isPlaying)
            {
                DrawPlayingMode();
            }
            else
            {
                if (_avatarAnimator != null && _avatarAnimator.runtimeAnimatorController == _runtimeAnimatorController)
                    _avatarAnimator.runtimeAnimatorController = null;

                DrawDescriptorPage();
                DrawSetupPage();
            }

            FocusAvatarWhenGetDescriptor();
        }

        [MenuItem("Tools/YuebyTools/VRChat/Avatar/DressingTool", false, -23)]
        public static void OpenWindow()
        {
            _window = GetWindow<DressingToolEditorWindow>();
            _window.titleContent = new GUIContent(_localization.Get("title_main_label"));
            _window.minSize = new Vector2(500, 700);
        }

        private string[] GetToolBarNames()
        {
            var toolBar = new List<string>
            {
                _localization.Get("option_test_t_pose_button"),
                _localization.Get("option_test_idle_pose_button"),
                _localization.Get("option_test_walk_1_pose_button"),
                _localization.Get("option_test_walk_2_pose_button"),
                _localization.Get("option_test_run_pose_button"),
                _localization.Get("option_test_prone_pose_button"),
                _localization.Get("option_test_custom1_pose_button"),
                _localization.Get("option_test_custom2_pose_button"),
                _localization.Get("option_test_custom3_pose_button"),
                _localization.Get("option_test_custom4_pose_button"),
                _localization.Get("option_test_custom5_pose_button"),
                _localization.Get("option_test_custom6_pose_button")
            };

            return toolBar.ToArray();
        }

        private void FocusAvatarWhenGetDescriptor()
        {
            if (Event.current.type == EventType.Repaint && _descriptor == null)
            {
                GetDescriptorWhenChangePlayMode();
                _isFocusAvatar = false;
            }

            if (!_isFocusAvatar)
            {
                if (_descriptor != null)
                    EditorUtils.WaitToDo(200, "FocusTarget", () => { EditorUtils.FocusTarget(_descriptor.gameObject); });
                _isFocusAvatar = true;
            }
        }

        private void DrawPlayingMode()
        {
            if (_descriptor != null)
            {
                _avatarAnimator = _descriptor.GetComponent<Animator>();
                if (_runtimeAnimatorController != null)
                {
                    if (_avatarAnimator.runtimeAnimatorController == null)
                    {
                        _avatarAnimator.runtimeAnimatorController = _runtimeAnimatorController;
                        EditorUtils.FocusTarget(_descriptor.gameObject);
                    }
                    else
                    {
                        if (_avatarAnimator.runtimeAnimatorController != _runtimeAnimatorController)
                        {
                            _avatarAnimator.runtimeAnimatorController = _runtimeAnimatorController;
                            EditorUtils.FocusTarget(_descriptor.gameObject);
                        }
                    }
                }

                EditorUI.VerticalEGLTitled(_localization.Get("title_configure_label"), () =>
                {
                    EditorGUI.BeginDisabledGroup(true);
                    _descriptor = (VRCAvatarDescriptor)EditorUI.ObjectFieldVertical(_descriptor, _localization.Get("configure_avatar_label"), typeof(VRCAvatarDescriptor));
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.HelpBox(_localization.Get("option_test_testing_tip"), MessageType.Info);
                });

                EditorUI.VerticalEGLTitled(_localization.Get("title_setup_label"), () =>
                {
                    if (_avatarAnimator != null && _avatarAnimator.runtimeAnimatorController != null)
                    {
                        var toolBarNames = GetToolBarNames();

                        EditorGUI.BeginChangeCheck();
                        _toolBarSelectionIndex = GUILayout.SelectionGrid(_toolBarSelectionIndex, toolBarNames, 6);
                        if (EditorGUI.EndChangeCheck())
                        {
                            var stringHash = Animator.StringToHash(_poseName[_toolBarSelectionIndex]);
                            _avatarAnimator.CrossFade(stringHash, 0.2f);
                        }

                        // var toolBarNames = GetToolBarNames();
                        // var avatarAnimatorInteger = _avatarAnimator.GetInteger(_poseInt);
                        // var parameter = GUILayout.Toolbar(avatarAnimatorInteger, toolBarNames);
                        // _avatarAnimator.SetInteger(_poseInt, parameter);
                    }

                    if (GUILayout.Button(_localization.Get("option_test_stop_button")))
                        if (EditorApplication.isPlaying)
                            EditorApplication.isPlaying = false;
                });
            }
        }

        private void GetDescriptorWhenChangePlayMode()
        {
            if (EditorPrefs.HasKey(DescriptorPrefs))
            {
                var descriptorId = EditorPrefs.GetInt(DescriptorPrefs);
                var descriptor = EditorUtility.InstanceIDToObject(descriptorId);
                if (descriptor != null) _descriptor = (VRCAvatarDescriptor)descriptor;
            }
        }

        private void OnClothesChanged()
        {
            if (_clothes == null) return;
            if (_preClothes != null)
                _preClothes.SetActive(false);

            _preClothes = _clothes;

            if (!_clothes.gameObject.activeSelf)
                _clothes.gameObject.SetActive(true);

            if (_clothes != null)
            {
                if (_isAutoParentKeywordObjectRoot)
                {
                    var keywordGo = _descriptor.transform.Find(_autoParentKeyword);
                    if (keywordGo != null)
                    {
                        var checkClothes = keywordGo.transform.Find(_clothes.name);
                        _hasSameNameClothes = checkClothes != null;
                    }
                }
                else
                {
                    var checkClothes = _descriptor.transform.Find(_clothes.name);
                    _hasSameNameClothes = checkClothes != null;
                }
            }

            _isSureDressing = false;
            _isDuplicateDress = true;
            _useClothesName = true;
        }

        private void OnDescriptorChanged()
        {
            if (_descriptor == null)
            {
                EditorPrefs.DeleteKey(DescriptorPrefs);
                return;
            }

            EditorPrefs.SetInt(DescriptorPrefs, _descriptor.GetInstanceID());
            if (_preDescriptorGo != null)
                _preDescriptorGo.SetActive(false);

            _preDescriptorGo = _descriptor.gameObject;

            if (!_descriptor.gameObject.activeSelf)
                _descriptor.gameObject.SetActive(true);

            if (_clothes != null)
            {
                if (_isAutoParentKeywordObjectRoot)
                {
                    var keywordGo = _descriptor.transform.Find(_autoParentKeyword);
                    if (keywordGo != null)
                    {
                        var checkClothes = keywordGo.transform.Find(_clothes.name);
                        _hasSameNameClothes = checkClothes != null;
                    }
                }
                else
                {
                    var checkClothes = _descriptor.transform.Find(_clothes.name);
                    _hasSameNameClothes = checkClothes != null;
                }
            }

            EditorUtils.FocusTarget(_descriptor.gameObject);
        }

        private void DrawSetupPage()
        {
            if (_descriptor == null) return;

            if (_clothes == null)
            {
                EditorUI.VerticalEGLTitled(_localization.Get("title_setup_label"), () =>
                {
                    EditorUI.HorizontalEGL(() =>
                    {
                        if (GUILayout.Button(_localization.Get("option_test_change_path"), GUILayout.Width(100)))
                        {
                            var animPath = GetAnimationAssetsPath();

                            // if (!Directory.Exists("DressingTools"))
                            //     AssetDatabase.CreateFolder("Assets", "DressingTools");
                            EditorUtils.MoveFolderFromPath(ref animPath, "DressingTools/Animation");

                            _path = $"{animPath}/AvatarTest";
                        }

                        if (GUILayout.Button(_localization.Get("option_test_jump_path"), GUILayout.Width(100)))
                        {
                            EditorUtils.PingProject(_path);
                        }

                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.TextField(_path);
                        EditorGUI.EndDisabledGroup();
                    });
                    if (GUILayout.Button(_localization.Get("option_test_button")))
                        //Test
                        TestAvatar();
                });
            }
            else
            {
                if (!_isSureDressing && _hasSameNameClothes) return;
                EditorUI.VerticalEGLTitled(_localization.Get("title_setup_label"), () =>
                {
                    _isAutoParentKeywordObjectRoot = EditorUI.Radio(_isAutoParentKeywordObjectRoot, _localization.Get("option_auto_parent_keyword_radio"));

                    if (_isAutoParentKeywordObjectRoot)
                        EditorUI.DrawChildElement(1, () =>
                        {
                            EditorGUI.BeginChangeCheck();

                            _autoParentKeyword = EditorUI.TextField(_localization.Get("option_keyword_label"), _autoParentKeyword, 60);
                            if (EditorGUI.EndChangeCheck())
                                if (_clothes != null)
                                {
                                    var keywordGo = _descriptor.transform.Find(_autoParentKeyword);
                                    if (keywordGo != null)
                                    {
                                        var checkClothes = keywordGo.transform.Find(_clothes.name);
                                        _hasSameNameClothes = checkClothes != null;
                                    }
                                }

                            EditorGUILayout.HelpBox(string.Format(_localization.Get("option_none_keyword_object_tip"), _autoParentKeyword), MessageType.Info);
                        });

                    _isDuplicateDress = EditorUI.Radio(_isDuplicateDress, _localization.Get("option_create_copy_radio"));
                    if (!_isDuplicateDress)
                    {
                        EditorGUILayout.HelpBox(_localization.Get("option_delete_undo_tip"), MessageType.Info);
                        EditorGUILayout.Space();
                    }
                    // YuebyUtil.Line();

                    _isExtractPb = EditorUI.Radio(_isExtractPb, _localization.Get("option_combine_physbone_radio"));
                    if (!_isExtractPb)
                    {
                        EditorGUILayout.HelpBox(_localization.Get("option_combine_physbone_tip"), MessageType.Info);
                        EditorGUILayout.Space();
                    }

                    _useClothesName = EditorUI.Radio(_useClothesName, _localization.Get("option_suffix_radio"));
                    var beLikeName = _useClothesName ? $" ({_clothes.name})" : "";
                    EditorGUILayout.HelpBox($"{_localization.Get("option_bone_will_named_tip")} :\n Hips {beLikeName}", MessageType.Info);

                    EditorGUILayout.Space(5);
                    EditorUI.HorizontalEGL(() =>
                    {
                        if (GUILayout.Button(_localization.Get("option_dress_button")))
                            Dress();

                        if (GUILayout.Button(_localization.Get("option_test_button")))
                            //Test
                            TestAvatar();
                    });
                });
            }
        }

        private void DrawDescriptorPage()
        {
            EditorUI.VerticalEGLTitled(_localization.Get("title_configure_label"), () =>
            {
                EditorUI.HorizontalEGL(() =>
                {
                    EditorUI.DrawCheckChanged(() =>
                    {
                        //
                        _descriptor = (VRCAvatarDescriptor)EditorUI.ObjectFieldVertical(_descriptor, _localization.Get("configure_avatar_label"), typeof(VRCAvatarDescriptor));
                    }, OnDescriptorChanged);

                    if (_descriptor != null)
                    {
                        EditorUI.Line(LineType.Vertical);
                        EditorGUI.BeginChangeCheck();
                        _clothes = (GameObject)EditorUI.ObjectFieldVertical(_clothes, _localization.Get("configure_clothes_label"), typeof(GameObject));
                        if (EditorGUI.EndChangeCheck())
                            OnClothesChanged();
                    }
                }, GUILayout.MaxHeight(40));
                if (_descriptor == null)
                {
                    EditorGUILayout.HelpBox(_localization.Get("configure_avatar_tip"), MessageType.Info);
                }
                else
                {
                    if (_clothes == null)
                    {
                        EditorGUILayout.HelpBox(_localization.Get("configure_clothes_tip"), MessageType.Info);
                    }
                    else
                    {
                        if (!_hasSameNameClothes)
                        {
                            var checkClothes = _descriptor.transform.Find(_clothes.name);
                            _hasSameNameClothes = checkClothes != null;
                        }

                        if (_hasSameNameClothes)
                        {
                            if (_isSureDressing)
                                if (_isValidClothes && !_isBoneNameWrong && _descriptor.gameObject != _clothes)
                                    EditorUI.GoodHelpBox(_localization.Get("configure_check_over_tip"));
                        }
                        else
                        {
                            if (_isValidClothes && !_isBoneNameWrong && _descriptor.gameObject != _clothes)
                                EditorUI.GoodHelpBox(_localization.Get("configure_check_over_tip"));
                        }

                        // if (_isValidClothes && !_isBoneNameWrong && _descriptor.gameObject != _clothes)
                        // {
                        //     YuebyUtil.GoodHelpBox(DressingLocalization.Instance.GetValue(DressingLabelType.ClothesAllRightHelpBox));
                        // }

                        if (_hasSameNameClothes && _isValidClothes)
                        {
                            EditorGUILayout.HelpBox(_localization.Get("configure_same_name_tip"), MessageType.Warning);
                            _isSureDressing = EditorUI.Radio(_isSureDressing, _localization.Get("configure_sure_radio"));
                        }

                        if (_descriptor.gameObject == _clothes)
                            EditorGUILayout.HelpBox(_localization.Get("configure_same_game_object_tip"), MessageType.Error);

                        var armatureErrorInfo = string.Empty;
                        _isValidClothes = IsValidArmature(_descriptor.gameObject, _clothes, error => armatureErrorInfo = error);
                        if (!_isValidClothes)
                        {
                            EditorGUILayout.HelpBox(armatureErrorInfo, MessageType.Error);
                        }
                        else
                        {
                            _isBoneNameWrong = IsBoneNameWrong(_descriptor.gameObject, _clothes);

                            if (_isBoneNameWrong)
                            {
                                EditorGUILayout.HelpBox(_localization.Get("configure_bone_name_issue_tip"), MessageType.Warning);
                                if (GUILayout.Button(_localization.Get("configure_try_fix_button"))) TryFixBoneName(_descriptor.gameObject, _clothes);
                            }
                        }
                    }
                }
            });
        }

        private void Dress()
        {
            var dressAvatar = Instantiate(_descriptor.gameObject);
            dressAvatar.name = _descriptor.name;

            var dressClothes = Instantiate(_clothes, dressAvatar.transform, true);
            dressClothes.name = _clothes.name;
           

            if (_isAutoParentKeywordObjectRoot)
            {
                var clothesObjTrans = dressAvatar.transform.Find(_autoParentKeyword);
                if (clothesObjTrans == null)
                {
                    clothesObjTrans = new GameObject(_autoParentKeyword)
                    {
                        transform =
                        {
                            parent = dressAvatar.transform
                        }
                    }.transform;

                    Undo.RegisterCreatedObjectUndo(clothesObjTrans.gameObject, "clothesObjTrans");
                }

                dressClothes.transform.SetParent(clothesObjTrans);
            }

            var anim = dressClothes.GetComponent<Animator>();
            if (anim)
                if (EditorUtility.DisplayDialog(_localization.Get("dialog_animator_check_title"), _localization.Get("dialogue_animator_check_content"), _localization.Get("dialogue_yes_button"), _localization.Get("dialogue_no_button")))
                    DestroyImmediate(anim);

            Undo.RegisterCreatedObjectUndo(dressAvatar, "dressAvatar");
            Undo.RegisterCreatedObjectUndo(dressClothes, "dressClothes");

            if (!_isDuplicateDress)
                Undo.DestroyObjectImmediate(_descriptor.gameObject);
            else
                _descriptor.gameObject.SetActive(false);

            _clothes.gameObject.SetActive(false);
            _clothes = null;

            _avatarArmature = dressAvatar.transform.Find("Armature");
            if (_avatarArmature == null)
                _avatarArmature = dressAvatar.transform.Find("armature");

            _clothesArmature = dressClothes.transform.Find("Armature");
            if (_clothesArmature == null)
                _clothesArmature = dressClothes.transform.Find("armature");

            dressAvatar.SetActive(true);
            dressClothes.SetActive(true);
            _descriptor = dressAvatar.GetComponent<VRCAvatarDescriptor>();

            // 获得衣服Physbone，准备合并
            _waitCombinePhysBones = new List<VRCPhysBone>();
            var clothChildTransList = _clothesArmature.GetComponentsInChildren<Transform>(true);
            foreach (var child in clothChildTransList)
            {
                var pb = child.GetComponent<VRCPhysBone>();
                if (!pb) continue;
                if (!pb.rootTransform)
                    pb.rootTransform = child;

                _waitCombinePhysBones.Add(pb);
            }

            CombinePhysBone(dressClothes.transform);
            MoveBone(_avatarArmature, _clothesArmature, dressClothes.name);
           

            if (_clothesArmature.childCount == 0)
                Undo.DestroyObjectImmediate(_clothesArmature.gameObject);

            EditorUtils.FocusTarget(_descriptor.gameObject);
            _preClothes = dressClothes;
            
            GUIUtility.ExitGUI();
        }

        private void TestAvatar()
        {
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;

            EditorPrefs.SetInt(DescriptorPrefs, _descriptor.GetInstanceID());
        }

        private bool IsValidArmature(GameObject avatar, GameObject clothes, UnityAction<string> error)
        {
            var avatarArmature = avatar.transform.Find("Armature");
            if (!avatarArmature)
                avatarArmature = avatar.transform.Find("armature");

            var clothesArmature = clothes.transform.Find("Armature");
            if (!clothesArmature)
                clothesArmature = clothes.transform.Find("armature");

            if (!avatarArmature || !clothesArmature)
            {
                error?.Invoke(_localization.Get("configure_armature_issue_tip"));
                return false;
            }

            if (avatarArmature.childCount == 0 || clothesArmature.childCount == 0)
            {
                error?.Invoke(_localization.Get("configure_armature_none_bone_tip"));
                return false;
            }

            error?.Invoke(string.Empty);
            return true;
        }

        private void TryFixBoneName(GameObject avatar, GameObject clothes)
        {
            var newClothes = Instantiate(clothes, clothes.transform.position, clothes.transform.rotation, clothes.transform.parent);
            clothes.SetActive(false);

            var avatarChildList = avatar.GetComponentsInChildren<Transform>(true).ToList();
            var clothesChildList = newClothes.GetComponentsInChildren<Transform>(true).ToList();

            foreach (var t in avatarChildList)
            foreach (var clothesChild in clothesChildList.Where(clothesChild => clothesChild.name.Contains(t.name)))
            {
                clothesChild.name = t.name;
                break;
            }

            newClothes.name = _clothes.name + " (Fix Name)";
            _clothes = newClothes;
            _clothes.SetActive(true);
        }

        private bool IsBoneNameWrong(GameObject avatar, GameObject clothes)
        {
            var avatarArmature = avatar.transform.Find("Armature");
            if (!avatarArmature)
                avatarArmature = avatar.transform.Find("armature");

            var clothesArmature = clothes.transform.Find("Armature");
            if (!clothesArmature)
                clothesArmature = avatar.transform.Find("armature");

            return avatarArmature.GetChild(0).name != clothesArmature.GetChild(0).name;
        }

        /// <summary>
        ///     移动骨骼
        /// </summary>
        /// <param name="avatar">avatar</param>
        /// <param name="clothes">clothes</param>
        /// <param name="clothesNameSuffix">衣服名字后缀</param>
        /// <returns></returns>
        private bool MoveBone(Transform avatar, Transform clothes, string clothesNameSuffix)
        {
            var clothesChildList = new List<Transform>();

            // 获得衣服所有子对象
            for (var i = 0; i < clothes.childCount; i++) clothesChildList.Add(clothes.GetChild(i));

            foreach (var clothesChild in clothesChildList)
            {
                var avatarBone = avatar.Find(clothesChild.name);
                // 获得衣服child的PhysBone
                var childPb = clothesChild.GetComponent<VRCPhysBone>();

                // 如果avatar的骨骼不为空
                if (avatarBone != null)
                {
                    var avtrPb = avatarBone.GetComponent<VRCPhysBone>();
                    // 查找该骨骼有没有PhysBone组件
                    if (avtrPb)
                    {
                        // 销毁
                        if (childPb)
                        {
                            if (_waitCombinePhysBones != null && _waitCombinePhysBones.Count > 0)
                                if (_waitCombinePhysBones.Contains(childPb))
                                    _waitCombinePhysBones.Remove(childPb);

                            DestroyImmediate(childPb);
                        }

                        // 添加父对象约束
                        AddParentConstraints(avatarBone, clothesChild);
                    }
                    else
                    {
                        // 没有Physbone就设置为父对象直接加入作为子对象
                        clothesChild.transform.SetParent(avatarBone);
                        clothesChild.name += $" ({clothesNameSuffix})";

                        if (!MoveBone(avatarBone, clothesChild, clothesNameSuffix)) return false;
                    }
                }
            }

            return true;
        }

        private void CombinePhysBone(Transform clothes)
        {
            if (_isExtractPb)
            {
                var drawer = new PhysBoneExtractorDrawer();
                drawer.Data.Target = clothes.gameObject;
                PhysBoneExtractor.Extract(drawer, true);

                // var pbTrans = clothes.Find(PhysBoneExtractor.PhyBoneRootName);
                // if (!pbTrans)
                // {
                //     pbTrans = new GameObject(PhysBoneExtractor.PhyBoneRootName) { transform = { parent = clothes } }.transform;
                //     Undo.RegisterCreatedObjectUndo(pbTrans.gameObject, "Pb Trans go");
                // }
                //
                // foreach (var physbone in _waitCombinePhysBones)
                // {
                //     var pasteTarget = pbTrans.gameObject;
                //
                //     Debug.Log(_isSplitCombine);
                //     if (_isSplitCombine)
                //     {
                //         Debug.Log("111");
                //         pasteTarget = new GameObject(physbone.name + "_PB") { transform = { parent = pbTrans } };
                //     }
                //
                //     ComponentUtility.CopyComponent(physbone);
                //     ComponentUtility.PasteComponentAsNew(pasteTarget);
                //     DestroyImmediate(physbone);
                // }
            }
        }

        private void AddParentConstraints(Transform avatar, Transform clothes)
        {
            var parentConstraint = clothes.gameObject.AddComponent<ParentConstraint>();
            parentConstraint.constraintActive = true;

            var source = new ConstraintSource
            {
                sourceTransform = avatar,
                weight = 1
            };
            parentConstraint.AddSource(source);

            var childs = new List<Transform>();

            for (var i = 0; i < clothes.childCount; i++) childs.Add(clothes.GetChild(i));

            foreach (var child in childs)
            {
                var avatarBone = avatar.Find(child.name);
                if (avatarBone != null) AddParentConstraints(avatarBone, child);
            }
        }
    }
}