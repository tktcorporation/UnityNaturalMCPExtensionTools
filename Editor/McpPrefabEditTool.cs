using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;


namespace UnityNaturalMCPExtension.Editor
{
    [McpServerToolType, Description("Prefab編集モード管理ツール")]
    internal sealed class McpPrefabEditTool
    {
        [McpServerTool, Description("Prefabを編集モードで開く")]
        public async ValueTask<string> OpenPrefabMode(
            [Description("Prefabアセットのパス (例: Assets/Prefabs/MyPrefab.prefab)")] string prefabPath)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // 既にPrefabモードが開いているか確認
                var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (currentStage != null)
                {
                    if (currentStage.assetPath == prefabPath)
                    {
                        return $"Prefab '{prefabPath}' は既に編集モードで開いています";
                    }
                    else
                    {
                        // 別のPrefabが開いている場合は閉じる
                        StageUtility.GoBackToPreviousStage();
                    }
                }

                // Prefabアセットの存在確認
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    return $"エラー: Prefab '{prefabPath}' が見つかりません";
                }

                // PrefabModeで開く
                var prefabStage = PrefabStageUtility.OpenPrefab(prefabPath);
                if (prefabStage == null)
                {
                    return $"エラー: Prefab '{prefabPath}' を編集モードで開けませんでした";
                }

                // 編集可能なルートオブジェクトの情報を返す
                var root = prefabStage.prefabContentsRoot;
                return $"Prefab '{prefabPath}' を編集モードで開きました。ルートオブジェクト: '{root.name}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"OpenPrefabMode エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("Prefab編集モードで変更を保存")]
        public async ValueTask<string> SavePrefabMode()
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    return "エラー: Prefab編集モードが開いていません";
                }

                // 変更があるか確認 - scene.isDirtyを使用（公開API）
                if (!prefabStage.scene.isDirty)
                {
                    return "変更はありません";
                }

                // 変更を保存
                var assetPath = prefabStage.assetPath;
                var root = prefabStage.prefabContentsRoot;

                // Prefabとして保存
                bool success = false;

                try
                {
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    success = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error saving prefab asset: {e.Message}\n{e.StackTrace}");
                    success = false;
                }

                if (success)
                {
                    EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
                    AssetDatabase.SaveAssets();
                    return $"Prefab '{assetPath}' の変更を保存しました";
                }
                else
                {
                    return $"エラー: Prefabの保存に失敗しました";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SavePrefabMode エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("Prefab編集モードを閉じる")]
        public async ValueTask<string> ClosePrefabMode(
            [Description("保存せずに閉じる場合はtrue (デフォルト: false)")] bool discardChanges = false)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    return "Prefab編集モードは開いていません";
                }

                var assetPath = prefabStage.assetPath;

                // 変更がある場合の処理（変更検出は省略し、discardChangesフラグがfalseなら保存）
                if (!discardChanges)
                {
                    // 自動保存
                    var saveResult = await SavePrefabMode();
                    Debug.Log($"自動保存: {saveResult}");
                }

                // Prefabモードを閉じる
                StageUtility.GoBackToPreviousStage();

                var message = discardChanges
                    ? $"Prefab '{assetPath}' の編集モードを閉じました（変更は破棄されました）"
                    : $"Prefab '{assetPath}' の編集モードを閉じました";

                return message;
            }
            catch (Exception e)
            {
                Debug.LogError($"ClosePrefabMode エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("現在のPrefab編集モードの状態を取得")]
        public async ValueTask<string> GetPrefabModeStatus()
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    return "Prefab編集モードは開いていません";
                }

                var root = prefabStage.prefabContentsRoot;
                var assetPath = prefabStage.assetPath;
                var isModified = prefabStage.scene.isDirty; // PrefabStageの正しい変更検出（公開API）

                var status = $"編集中のPrefab: '{assetPath}'\n";
                status += $"ルートオブジェクト: '{root.name}'\n";
                status += $"子オブジェクト数: {root.transform.childCount}\n";
                status += $"変更状態: {(isModified ? "変更あり" : "変更なし")}\n";

                // コンポーネント情報
                var components = root.GetComponents<UnityEngine.Component>();
                status += $"コンポーネント数: {components.Length}\n";
                foreach (var comp in components)
                {
                    if (comp != null && !(comp is Transform))
                    {
                        status += $"  - {comp.GetType().Name}\n";
                    }
                }

                return status;
            }
            catch (Exception e)
            {
                Debug.LogError($"GetPrefabModeStatus エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }
    }
}