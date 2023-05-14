using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RPGMaker.Codebase.Addon;
using UnityEngine;
using UnityEngine.UIElements;
using RPGMaker.Codebase.CoreSystem.Knowledge.DataModel.Map;
using RPGMaker.Codebase.CoreSystem.Knowledge.DataModel.EventMap;
using RPGMaker.Codebase.CoreSystem.Lib.Auth;
using RPGMaker.Codebase.Runtime.Addon;
using RPGMaker.Codebase.Editor.Hierarchy.Common;
using RPGMaker.Codebase.Editor.Hierarchy.Region.Map.View;
using RPGMaker.Codebase.Editor.Common;
using RPGMaker.Codebase.Editor.Hierarchy;
using RPGMaker.Codebase.Editor.MapEditor;
using RPGMaker.Codebase.Editor.MapEditor.Component.Canvas;
using RPGMaker.Codebase.Editor.Hierarchy.Enum;
using UnityEditor;

public static class FasterLoadingEditor
{
    public static void Execute()
    {
        SwapMethods(typeof(FasterLoadingEditor),
            nameof(CreateMapFoldoutContents),
            typeof(CommonMapHierarchyView),
            "CreateMapFoldoutContents");

        SwapMethods(typeof(FasterLoadingEditor),
            nameof(WindowMaximizationRecoveringProcess),
            typeof(RpgMakerEditor),
            "WindowMaximizationRecoveringProcess");
    }

    #region Reflection Helpers

    public class ReflectionHelper
    {
        private ReflectionCache reflectionCache;

        private class ReflectionCache
        {
            private Dictionary<string, MethodInfo> methodInfoMap = new Dictionary<string, MethodInfo>();
            private Dictionary<string, FieldInfo> fieldInfoMap = new Dictionary<string, FieldInfo>();

            public ReflectionCache()
            {
            }

            public MethodInfo GetCachedMethodInfo(string methodName, BindingFlags? flags)
            {
                MethodInfo ret;
                if (flags == null)
                {
                    methodInfoMap.TryGetValue(methodName, out ret);
                }
                else
                {
                    methodInfoMap.TryGetValue(methodName + ((int)flags.Value).ToString(), out ret);
                }

                return ret;
            }

            public void SetCachedMethod(string methodName, MethodInfo method, BindingFlags? flags)
            {
                if (flags.HasValue)
                    methodInfoMap[methodName + ((int)flags.Value).ToString()] = method;
                else
                    methodInfoMap[methodName] = method;
            }

            public void SetCachedField(string fieldName, FieldInfo field, BindingFlags? flags)
            {
                if (flags.HasValue)
                    fieldInfoMap[fieldName + ((int)flags.Value).ToString()] = field;
                else
                    fieldInfoMap[fieldName] = field;
            }

            public FieldInfo GetCachedFieldInfo(string fieldName, BindingFlags? flags)
            {
                FieldInfo ret;
                if (flags == null)
                {
                    fieldInfoMap.TryGetValue(fieldName, out ret);
                }
                else
                {
                    fieldInfoMap.TryGetValue(fieldName + ((int)flags.Value).ToString(), out ret);
                }

                return ret;
            }
        }

        private static Dictionary<Type, ReflectionHelper> instances = new Dictionary<Type, ReflectionHelper>();

        public static ReflectionHelper GetInstance<T>()
        {
            if (!instances.ContainsKey(typeof(T)))
            {
                instances[typeof(T)] = new ReflectionHelper(typeof(T));
            }

            return instances[typeof(T)];
        }

        private Type type;

        public ReflectionHelper(Type type)
        {
            this.type = type;
            reflectionCache = new ReflectionCache();
        }

        private ReflectionCache GetCache()
        {
            return reflectionCache;
        }

        public FieldInfo GetField(string fieldName)
        {
            FieldInfo ret = GetCache().GetCachedFieldInfo(fieldName, null);
            if (ret != null)
                return ret;
            ret = type.GetField(fieldName);
            if (ret == null)
            {
                var fields = type.GetFields(BindingFlags.Public |
                                            BindingFlags.NonPublic |
                                            BindingFlags.Static |
                                            BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.Name == fieldName)
                    {
                        Debug.LogWarning(
                            $"Update {type.Name}::GetField({fieldName}) code to include binding flags {field.Attributes.ToString()}");
                        ret = field;
                        break;
                    }
                }
            }

            if (ret == null)
            {
                throw new Exception($"Failed to find field {fieldName} in {type}");
            }

            GetCache().SetCachedField(fieldName, ret, null);
            return ret;
        }

        public FieldInfo GetField(string fieldName, BindingFlags bindingFlags)
        {
            var ret = GetCache().GetCachedFieldInfo(fieldName, bindingFlags);
            if (ret != null)
                return ret;
            ret = type.GetField(fieldName, bindingFlags);
            if (ret == null)
            {
                throw new Exception($"Failed to find field {fieldName} with flags {bindingFlags} in {type}");
            }

            GetCache().SetCachedField(fieldName, ret, bindingFlags);
            return ret;
        }

        public MethodInfo GetMethod(string methodName)
        {
            var ret = GetCache().GetCachedMethodInfo(methodName, null);
            if (ret != null)
                return ret;
            ret = type.GetMethod(methodName);
            if (ret == null)
            {
                var methods = type.GetMethods(BindingFlags.Public |
                                              BindingFlags.NonPublic |
                                              BindingFlags.Static |
                                              BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name == methodName)
                    {
                        Debug.LogWarning(
                            $"Update {type.Name}::GetMethod({methodName}) code to include binding flags {method.Attributes}");
                        return method;
                    }
                }

                throw new Exception($"Failed to find method {methodName} in {type}");
            }

            GetCache().SetCachedMethod(methodName, ret, null);
            return ret;
        }

        public MethodInfo GetMethod(string methodName, BindingFlags bindingFlags)
        {
            var ret = GetCache().GetCachedMethodInfo(methodName, bindingFlags);
            if (ret != null)
                return ret;
            ret = type.GetMethod(methodName, bindingFlags);
            if (ret == null)
            {
                Debug.LogError($"Failed to find method {methodName} with flags {bindingFlags} in {type}");
                throw null;
            }

            GetCache().SetCachedMethod(methodName, ret, bindingFlags);
            return ret;
        }
    }

    public static void SwapMethods(Type myType, string myMethodName, Type theirType, string theirMethodName)
    {
        MethodInfo myMethod = myType.GetMethod(myMethodName);
        if (myMethod == null)
        {
            throw new System.ArgumentException($"Failed to find method {myType.Name}::{myMethodName}.");
        }

        MethodInfo theirMethod = theirType.GetMethod(theirMethodName);
        if (theirMethod == null)
        {
            throw new System.ArgumentException($"Failed to find method {theirType.Name}::{theirMethodName}.");
        }

        AddonInstance.ExchangeFunctionPointer(myMethod, theirMethod);
    }

    #endregion

    #region New Methods

    public static void CreateMapFoldoutContents(
        MapDataModel mapEntity,
        IMapHierarchyInfo mapHierarchyInfo,
        MapHierarchyView mapHierarchyView,
        Foldout mapFoldout
    )
    {
        if (!FasterLoading.ShouldFixCreateMapFoldoutContents)
        {
            CreateMapFoldoutContents(mapEntity, mapHierarchyInfo, mapHierarchyView,
                mapFoldout); //Calls original function
            return;
        }

        var helper = ReflectionHelper.GetInstance<CommonMapHierarchyView>();
        //Expose private methods
        var copiedEventIdHandle =
            helper.GetField("copiedEventId",
                BindingFlags.Static |
                BindingFlags.NonPublic);
        var getActiveButtonNameByDeletingEventHandle = helper.GetMethod("GetActiveButtonNameByDeletingEvent",
            BindingFlags.Static |
            BindingFlags.NonPublic);
        var eventMapDataModelHandle =
            helper.GetField("eventMapDataModel",
                BindingFlags.Static |
                BindingFlags.NonPublic);
        var eventMapPageHandle =
            helper.GetField("eventMapPage",
                BindingFlags.Static |
                BindingFlags.NonPublic);
        if (copiedEventIdHandle == null)
        {
            Debug.LogWarning("CopiedEventIdHandle is null");
            return;
        }

        if (getActiveButtonNameByDeletingEventHandle == null)
        {
            Debug.LogWarning("GetActiveButtonNameByDeletingEventHandle is null");
            return;
        }

        if (eventMapDataModelHandle == null)
        {
            Debug.LogWarning("eventMapDataModelHandle is null");
            return;
        }

        if (eventMapPageHandle == null)
        {
            Debug.LogWarning("eventMapPageHandle is null");
            return;
        }

        // - - - マップ編集ボタン
        MapDataModel mapEntityWork = mapEntity;
        var btnEditMap = new Button { text = EditorLocalize.LocalizeText("WORD_0012") };
        btnEditMap.name = CommonMapHierarchyView.GetMapEditButtonName(mapEntity.id);
        btnEditMap.AddToClassList("button-transparent");
        btnEditMap.AddToClassList("AnalyticsTag__page_view__map_edit");
        Hierarchy.AddSelectableElementAndAction(btnEditMap,
            () => { MapEditor.LaunchMapEditMode(Hierarchy.mapManagementService.LoadMapById(mapEntity.id)); });
        btnEditMap.clicked += () => { Hierarchy.InvokeSelectableElementAction(btnEditMap); };
        mapFoldout.Add(btnEditMap);

#if TEST_PREVIE_SCENE_AGING
            DebugUtil.Execution(() =>
            {
                btnEditMap.clicked += () =>
                {
                    DebugUtil.EditorRepeatExecution(
                        () => { Hierarchy.InvokeSelectableElementAction(btnEditMap); },
                        "マップ編集",
                        100,
                        0.1f);
                };
            });
#endif

        // - - - バトル編集ボタン
        var btnEditBattle = new Button { text = EditorLocalize.LocalizeText("WORD_0013") };
        btnEditBattle.name = mapEntity.id + "_battle";
        btnEditBattle.AddToClassList("button-transparent");
        btnEditBattle.AddToClassList("AnalyticsTag__page_view__map_battle_edit");
        Hierarchy.AddSelectableElementAndAction(btnEditBattle,
            () => { MapEditor.LaunchBattleEditMode(mapEntity); });
        btnEditBattle.clicked += () => { Hierarchy.InvokeSelectableElementAction(btnEditBattle); };
        mapFoldout.Add(btnEditBattle);

#if TEST_PREVIE_SCENE_AGING
            DebugUtil.Execution(() =>
            {
                btnEditBattle.clicked += () =>
                {
                    DebugUtil.EditorRepeatExecution(
                        () => { Hierarchy.InvokeSelectableElementAction(btnEditBattle); },
                        "バトル編集",
                        100,
                        0.1f);
                };
            });
#endif

        // - - - "イベント" Foldout
        VisualElement foldoutElement = new VisualElement();

        var eventListFoldout = new Foldout();
        eventListFoldout.AddToClassList("AnalyticsTag__page_view__map_event");
        //eventListFoldout.value = false;
        eventListFoldout.name = mapFoldout.name + "_event";
        var foldoutLabel = new Label { text = EditorLocalize.LocalizeText("WORD_0014") };
        foldoutLabel.name = eventListFoldout.name + "_label";
        foldoutLabel.style.position = Position.Absolute;
        foldoutLabel.style.left = 35f;
        foldoutLabel.style.right = 2f;
        foldoutLabel.style.overflow = Overflow.Hidden;
        foldoutLabel.style.paddingTop = 2f;
        foldoutElement.Add(eventListFoldout);
        foldoutElement.Add(foldoutLabel);
        mapFoldout.Add(foldoutElement);
        mapHierarchyInfo.ParentClass.SetFoldout(eventListFoldout.name);

        foldoutLabel.RegisterCallback<ClickEvent>(evt =>
        {
            Hierarchy.InvokeSelectableElementAction(eventListFoldout);
            MapEditor.LaunchEventPutMode(mapEntity);
            mapHierarchyInfo.ExecEventType = ExecEventType.Here;
        });

        BaseClickHandler.ClickEvent(foldoutLabel, (evt) =>
        {
            if (evt != (int)MouseButton.RightMouse) return;
            var menu = new GenericMenu();

            // イベントの新規作成。
            menu.AddItem(
                new GUIContent(EditorLocalize.LocalizeText("WORD_0010")),
                false,
                () =>
                {
                    MapEditor.LaunchEventPutMode(mapEntity);
                    mapHierarchyInfo.ExecEventType = ExecEventType.Here;
                });
            menu.ShowAsContext();
        });

        Action ShowEventsCallback = () =>
        {
            var copyPageNum = 0;

            // - - - - イベント一覧
            foreach (var eventEntity in mapHierarchyInfo.EventMapDataModels.Where(e =>
                         e.mapId == mapEntity.id && e.pages.Count > 0))
            {
                // イベント名Foldout（ページを内包）
                var eventFoldout = new Foldout
                {
                    text = !string.IsNullOrEmpty(eventEntity.name)
                        ? eventEntity.name
                        : $"EV{mapHierarchyInfo.EventMapDataModels.IndexOf(eventEntity) + 1:000}"
                };
                eventFoldout.name = eventListFoldout.name + "_" + eventEntity.eventId;
                eventListFoldout.Add(eventFoldout);
                mapHierarchyInfo.ParentClass.SetFoldout(eventFoldout.name);

                BaseClickHandler.ClickEvent(eventFoldout, evt =>
                {
                    if (evt != (int)MouseButton.RightMouse) return;
                    var menu = new GenericMenu();

#if !DISABLE_EVENT_COPY_AND_PASTE
                    // イベントのコピー。
                    menu.AddItem(
                        new GUIContent(EditorLocalize.LocalizeText("WORD_0015")),
                        false,
                        () =>
                        {
                            copiedEventIdHandle.SetValue(null,
                                eventEntity.eventId); //CommonMapHierarchyView.copiedEventId = eventEntity.eventId;
                        });
#endif

                    // イベントの削除。
                    menu.AddItem(
                        new GUIContent(EditorLocalize.LocalizeText("WORD_0016")),
                        false,
                        () =>
                        {
                            var activeButtonName =
                                (string)getActiveButtonNameByDeletingEventHandle.Invoke(null,
                                    new object[] { eventEntity });

                            EventEditCanvas.CopyEventId = "";
                            MapEditor.DeleteEventMap(eventEntity);
                            _ = Hierarchy.Refresh(Region.Map, mapEntityWork.id);
                            _ = Hierarchy.Refresh(Region.Map, mapEntityWork.id, false);
                            Hierarchy.CompensateActiveButton(activeButtonName);
                        });

                    // EVページの新規作成。
                    menu.AddItem(new GUIContent(EditorLocalize.LocalizeText("WORD_0570")), false, () =>
                    {
                        mapHierarchyInfo.ExecEventType = ExecEventType.Here;
                        var pageNum = eventEntity.pages[eventEntity.pages.Count - 1].page + 1;
                        MapEditor.CreatePage(eventEntity, pageNum, 1);
                        mapHierarchyInfo.RefreshEventHierarchy(mapEntityWork.id);

                        // ヒエラルキーの該当イベントページを選択状態にする。
                        _ = Hierarchy.Refresh(Region.Map, mapEntityWork.id, false);
                        Hierarchy.SelectButton(
                            CommonMapHierarchyView.GetEventPageButtonName(eventEntity.eventId, pageNum));
                    });
                    // EVページの貼り付け
                    menu.AddItem(new GUIContent(EditorLocalize.LocalizeText("WORD_0571")), false, () =>
                    {
                        EventMapDataModel eventMapDataModel = (EventMapDataModel)eventMapDataModelHandle.GetValue(null);
                        EventMapDataModel.EventMapPage eventMapPage =
                            (EventMapDataModel.EventMapPage)eventMapPageHandle.GetValue(null);
                        if (eventMapDataModelHandle.GetValue(null) != null)
                        {
                            if (eventEntity != eventMapDataModel)
                                copyPageNum = eventEntity.pages[eventEntity.pages.Count - 1].page + 1;

                            MapEditor.CopyPage(eventMapDataModel, eventMapPage, copyPageNum, 1,
                                eventEntity);
                            _ = Hierarchy.Refresh(Region.Map, mapEntityWork.id, false);
                            mapHierarchyInfo.RefreshEventHierarchy(mapEntityWork.id);
                        }
                    });

                    menu.ShowAsContext();
                });

                if (eventEntity.pages == null || eventEntity.pages.Count == 0) return;

                // ページ一覧（クリックするとイベントエディタを開く）
                foreach (var page in eventEntity.pages)
                {
                    var pageNum = page.page;
                    var btnLoadEventPage = new Button
                        { text = EditorLocalize.LocalizeText("WORD_0019") + (page.page + 1) };
                    btnLoadEventPage.name = CommonMapHierarchyView.GetEventPageButtonName(eventEntity.eventId, pageNum);
                    btnLoadEventPage.AddToClassList("button-transparent");
                    Hierarchy.AddSelectableElementAndAction(btnLoadEventPage,
                        () => { MapEditor.LaunchEventEditMode(mapEntity, eventEntity, pageNum); });
                    btnLoadEventPage.AddToClassList(Hierarchy.ButtonTypeTag_WithEventSubWindows);

#if TEST_PREVIE_SCENE_AGING
                    DebugUtil.Execution(() =>
                    {
                        btnLoadEventPage.clicked += () =>
                        {
                            DebugUtil.EditorRepeatExecution(
                                () => { MapEditor.MapEditor.LaunchEventEditMode(mapEntity, eventEntity, pageNum); },
                                $"ページ{page.page + 1}",
                                100,
                                0.1f);
                        };
                    });
#endif

                    BaseClickHandler.ClickEvent(btnLoadEventPage, evt =>
                    {
                        if (evt == (int)MouseButton.RightMouse)
                        {
                            var menu = new GenericMenu();

                            // EVページのコピー。
                            menu.AddItem(new GUIContent(EditorLocalize.LocalizeText("WORD_0572")), false,
                                () =>
                                {
                                    mapHierarchyInfo.ExecEventType = ExecEventType.Here;
                                    eventMapDataModelHandle.SetValue(null,
                                        eventEntity); //CommonMapHierarchyView.eventMapDataModel = eventEntity;
                                    eventMapPageHandle.SetValue(null,
                                        page); // CommonMapHierarchyView.eventMapPage = page;
                                    copyPageNum = eventEntity.pages[eventEntity.pages.Count - 1].page + 1;
                                });

                            // EVページの削除。
                            menu.AddItem(new GUIContent(EditorLocalize.LocalizeText("WORD_0573")), false,
                                () =>
                                {
                                    var activeButtonName =
                                        (string)getActiveButtonNameByDeletingEventHandle.Invoke(null, new object[]
                                        {
                                            eventEntity, pageNum
                                        });
                                    //CommonMapHierarchyView.GetActiveButtonNameByDeletingEventPage(eventEntity, pageNum);

                                    eventMapDataModelHandle.SetValue(null,
                                        null); //CommonMapHierarchyView.eventMapDataModel = null;
                                    mapHierarchyInfo.ExecEventType = ExecEventType.Here;
                                    MapEditor.DeletePage(eventEntity, pageNum);

                                    mapHierarchyInfo.RefreshEventHierarchy(mapEntityWork.id);
                                    _ = Hierarchy.Refresh(Region.Map, mapEntityWork.id, false);
                                    Hierarchy.CompensateActiveButton(activeButtonName);
                                });

                            menu.ShowAsContext();
                        }
                        else
                        {
                            Hierarchy.InvokeSelectableElementAction(btnLoadEventPage);
                        }
                    });
                    eventFoldout.Add(btnLoadEventPage);
                }
            }
        };

        if (eventListFoldout.value)
        {
            ShowEventsCallback();
        }

        //Only build events when open
        eventListFoldout.RegisterValueChangedCallback((value) =>
        {
            if (value.newValue == false)
            {
                return;
            }

            if (eventListFoldout.Children().Count() > 0)
                return;
            ShowEventsCallback();
        });
    }


    public static void WindowMaximizationRecoveringProcess()
    {
        if (!FasterLoading.ShouldFixWindowMaximizationRecoveringProcess)
        {
            WindowMaximizationRecoveringProcess(); // Calls original function;
            return;
        }

        // 認証済でない場合は何もしない
        if (!Auth.IsAuthenticated)
        {
            return;
        }

        //最大化から復帰した場合には初期化を再実行
        //RPGMaker.Codebase.Editor.Hierarchy.Hierarchy.IsInitialized = false;
        RpgMakerEditor.RuntimeInitWindows(); //RuntimeInitWindows();

        // 以下を呼ばないと、RpgMakerEditor.SetWindows メソッドによって、
        // 『イベント実行内容』枠と『イベントコマンド』枠が閉じられてしまうので、呼ぶ。
        // シーン再生終了時も RuntimeInitWindows の後に呼ばれている。
        RpgMakerEditor.InitWindows(); //InitWindows();
    }

    #endregion
}