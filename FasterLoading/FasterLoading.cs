/*:
 * @addondesc Faster Loading
 * @author Agoaj
 * @help Improves editor load times.
 * version 0.0.1
 * 
 * @param fixCreateMapFoldout
 * @text Fix slow Event menu
 * @desc Fixes slow loading caused by creating menus for every event in the game.
 * @type boolean
 * @default true
 * @on Enable
 * @off Disable
 *
 * @param fixWindowMaximization
 * @text Fix extra window inits
 * @desc Fixes slow loading caused by reiniting the windows to fix fullscreen issues.
 * @type boolean
 * @default true
 * @on Enable
 * @off Disable
 */

/*:ja
 * @addondesc 読み込みの高速化
 * @author Agoaj
 * @help Version 0.0.1
 *
 * @param fixCreateMapFoldout
 * @text 遅いイベントメニューを修正
 * @desc ゲーム内のすべてのイベントのメニューを作成することによって引き起こされる読み込みの遅さを修正しました。
 * @type boolean
 * @default true
 * @on Enable
 * @off Disable
 *
 * @param fixWindowMaximization
 * @text 余分なウィンドウの初期化を修正
 * @desc フルスクリーンの問題を解決するために Windows を複数回再起動することによって引き起こされる読み込みの遅さを修正します。
 * @type boolean
 * @default true
 * @on Enable
 * @off Disable
 */
using System;
using System.Linq;
using UnityEngine;

namespace RPGMaker.Codebase.Addon
{
    public class FasterLoading
    {
        public static bool ShouldFixCreateMapFoldoutContents;
        public static bool ShouldFixWindowMaximizationRecoveringProcess;
        const string  EditorClassName = "FasterLoadingEditor";
        public FasterLoading(bool fixCreateMapFoldout, bool fixWindowMaximization)
        {
            ShouldFixCreateMapFoldoutContents = fixCreateMapFoldout;
            ShouldFixWindowMaximizationRecoveringProcess = fixWindowMaximization;
            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            System.Reflection.Assembly asm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(s => s.GetType(EditorClassName) != null);
            if (asm != null) 
            {
                var editorType = asm.GetType(EditorClassName);
                var execMethod = editorType.GetMethod("Execute");
                try
                {
                    execMethod.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to invoke {EditorClassName}::Execute. Unable to apply fixes.");
                    throw;
                }
            }
        }
    }
}
