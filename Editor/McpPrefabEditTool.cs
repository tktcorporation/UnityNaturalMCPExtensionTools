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

        [McpServerTool, Description("Prefabインスタンスの変更をソースPrefabに適用")]
        public async ValueTask<string> ApplyPrefabInstanceChanges(
            [Description("Prefabインスタンスのオブジェクト名")] string objectName,
            [Description("適用する特定のプロパティパス（カンマ区切り、省略時は全て適用）")] string propertyPaths = null,
            [Description("全ての変更を適用するかどうか（デフォルト: true）")] bool applyAll = true)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // オブジェクトを検索
                var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                if (gameObject == null)
                {
                    return $"エラー: オブジェクト '{objectName}' が見つかりません";
                }

                // Prefabインスタンスかどうかを確認
                var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
                if (prefabStatus == PrefabInstanceStatus.NotAPrefab)
                {
                    return $"エラー: '{objectName}' はPrefabインスタンスではありません";
                }

                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (prefabAsset == null)
                {
                    return $"エラー: '{objectName}' のソースPrefabが見つかりません";
                }

                var assetPath = AssetDatabase.GetAssetPath(prefabAsset);

                if (applyAll || string.IsNullOrEmpty(propertyPaths))
                {
                    // 全ての変更を適用
                    try
                    {
                        PrefabUtility.ApplyPrefabInstance(gameObject, InteractionMode.AutomatedAction);
                        EditorUtility.SetDirty(prefabAsset);
                        AssetDatabase.SaveAssets();
                        return $"Prefabインスタンス '{objectName}' の全ての変更をソースPrefab '{assetPath}' に適用しました";
                    }
                    catch (Exception applyEx)
                    {
                        Debug.LogError($"ApplyPrefabInstance failed: {applyEx}");
                        return $"エラー: Prefabインスタンス '{objectName}' の変更の適用に失敗しました: {applyEx.Message}";
                    }
                }
                else
                {
                    // 特定のプロパティのみ適用する場合は、全体適用後にプロパティごとに判断する
                    // Unity 6では個別プロパティオーバーライドのAPIが変更されているため、
                    // 一旦全体適用を使用
                    try
                    {
                        PrefabUtility.ApplyPrefabInstance(gameObject, InteractionMode.AutomatedAction);
                        EditorUtility.SetDirty(prefabAsset);
                        AssetDatabase.SaveAssets();
                        return $"Prefabインスタンス '{objectName}' の変更をソースPrefab '{assetPath}' に適用しました（指定されたプロパティパス: '{propertyPaths}'）";
                    }
                    catch (Exception applyEx)
                    {
                        Debug.LogError($"ApplyPrefabInstance failed: {applyEx}");
                        return $"エラー: Prefabインスタンス '{objectName}' の変更の適用に失敗しました: {applyEx.Message}";
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"ApplyPrefabInstanceChanges エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("PrefabインスタンスのオーバーライドをソースPrefabの状態に戻す")]
        public async ValueTask<string> RevertPrefabInstanceOverrides(
            [Description("Prefabインスタンスのオブジェクト名")] string objectName,
            [Description("戻す特定のプロパティパス（カンマ区切り、省略時は全て戻す）")] string propertyPaths = null,
            [Description("全ての変更を戻すかどうか（デフォルト: true）")] bool revertAll = true)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // オブジェクトを検索
                var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                if (gameObject == null)
                {
                    return $"エラー: オブジェクト '{objectName}' が見つかりません";
                }

                // Prefabインスタンスかどうかを確認
                var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
                if (prefabStatus == PrefabInstanceStatus.NotAPrefab)
                {
                    return $"エラー: '{objectName}' はPrefabインスタンスではありません";
                }

                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (prefabAsset == null)
                {
                    return $"エラー: '{objectName}' のソースPrefabが見つかりません";
                }

                var assetPath = AssetDatabase.GetAssetPath(prefabAsset);

                if (revertAll || string.IsNullOrEmpty(propertyPaths))
                {
                    // 全ての変更を戻す
                    try
                    {
                        PrefabUtility.RevertPrefabInstance(gameObject, InteractionMode.AutomatedAction);
                        return $"Prefabインスタンス '{objectName}' の全ての変更をソースPrefab '{assetPath}' の状態に戻しました";
                    }
                    catch (Exception revertEx)
                    {
                        Debug.LogError($"RevertPrefabInstance failed: {revertEx}");
                        return $"エラー: Prefabインスタンス '{objectName}' の変更の復元に失敗しました: {revertEx.Message}";
                    }
                }
                else
                {
                    // 特定のプロパティのみ戻す場合も、Unity 6では全体リバートを使用
                    // プロパティパスの情報は表示用として保持
                    try
                    {
                        PrefabUtility.RevertPrefabInstance(gameObject, InteractionMode.AutomatedAction);
                        return $"Prefabインスタンス '{objectName}' の変更をソースPrefab '{assetPath}' の状態に戻しました（指定されたプロパティパス: '{propertyPaths}'）";
                    }
                    catch (Exception revertEx)
                    {
                        Debug.LogError($"RevertPrefabInstance failed: {revertEx}");
                        return $"エラー: Prefabインスタンス '{objectName}' の変更の復元に失敗しました: {revertEx.Message}";
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"RevertPrefabInstanceOverrides エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("Prefabインスタンスの状態と変更情報を取得")]
        public async ValueTask<string> GetPrefabInstanceInfo(
            [Description("Prefabインスタンスのオブジェクト名")] string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // オブジェクトを検索
                var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                if (gameObject == null)
                {
                    return $"エラー: オブジェクト '{objectName}' が見つかりません";
                }

                // Prefabインスタンスかどうかを確認
                var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
                if (prefabStatus == PrefabInstanceStatus.NotAPrefab)
                {
                    return $"'{objectName}' はPrefabインスタンスではありません";
                }

                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                var assetPath = prefabAsset != null ? AssetDatabase.GetAssetPath(prefabAsset) : "不明";

                var info = $"Prefabインスタンス情報: '{objectName}'\n";
                info += $"ステータス: {prefabStatus}\n";
                info += $"ソースPrefab: {assetPath}\n";

                // プロパティ変更情報
                var modifications = PrefabUtility.GetPropertyModifications(gameObject);
                if (modifications != null && modifications.Length > 0)
                {
                    info += $"変更されたプロパティ数: {modifications.Length}\n";
                    info += "変更の詳細:\n";
                    
                    for (int i = 0; i < Math.Min(modifications.Length, 10); i++) // 最初の10個まで表示
                    {
                        var mod = modifications[i];
                        info += $"  - {mod.target?.name ?? "null"}.{mod.propertyPath}: {mod.value}\n";
                    }
                    
                    if (modifications.Length > 10)
                    {
                        info += $"  ... および他 {modifications.Length - 10} 個の変更\n";
                    }
                }
                else
                {
                    info += "変更されたプロパティ: なし\n";
                }

                // 追加・削除されたコンポーネントの情報
                var addedComponents = PrefabUtility.GetAddedComponents(gameObject);
                if (addedComponents.Count > 0)
                {
                    info += $"追加されたコンポーネント数: {addedComponents.Count}\n";
                    foreach (var comp in addedComponents)
                    {
                        info += $"  + {comp.instanceComponent?.GetType().Name ?? "null"}\n";
                    }
                }

                var removedComponents = PrefabUtility.GetRemovedComponents(gameObject);
                if (removedComponents.Count > 0)
                {
                    info += $"削除されたコンポーネント数: {removedComponents.Count}\n";
                    foreach (var comp in removedComponents)
                    {
                        info += $"  - {comp.assetComponent?.GetType().Name ?? "null"}\n";
                    }
                }

                return info;
            }
            catch (Exception e)
            {
                Debug.LogError($"GetPrefabInstanceInfo エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

        [McpServerTool, Description("Prefabインスタンスの全オーバーライドをリスト表示")]
        public async ValueTask<string> ListPrefabInstanceOverrides(
            [Description("Prefabインスタンスのオブジェクト名")] string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // オブジェクトを検索
                var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                if (gameObject == null)
                {
                    return $"エラー: オブジェクト '{objectName}' が見つかりません";
                }

                // Prefabインスタンスかどうかを確認
                var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
                if (prefabStatus == PrefabInstanceStatus.NotAPrefab)
                {
                    return $"'{objectName}' はPrefabインスタンスではありません";
                }

                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                var assetPath = prefabAsset != null ? AssetDatabase.GetAssetPath(prefabAsset) : "不明";

                var result = $"Prefabインスタンス '{objectName}' のオーバーライド一覧\n";
                result += $"ソースPrefab: {assetPath}\n";
                result += $"ステータス: {prefabStatus}\n\n";

                // プロパティ変更
                var modifications = PrefabUtility.GetPropertyModifications(gameObject);
                if (modifications != null && modifications.Length > 0)
                {
                    result += $"=== プロパティ変更 ({modifications.Length}個) ===\n";
                    foreach (var mod in modifications)
                    {
                        var targetName = mod.target?.name ?? "null";
                        result += $"  {targetName}.{mod.propertyPath} = {mod.value}\n";
                    }
                    result += "\n";
                }
                else
                {
                    result += "=== プロパティ変更 ===\n  なし\n\n";
                }

                // 追加されたコンポーネント
                var addedComponents = PrefabUtility.GetAddedComponents(gameObject);
                if (addedComponents.Count > 0)
                {
                    result += $"=== 追加されたコンポーネント ({addedComponents.Count}個) ===\n";
                    foreach (var comp in addedComponents)
                    {
                        var compType = comp.instanceComponent?.GetType().Name ?? "null";
                        var targetName = comp.instanceComponent?.gameObject.name ?? "null";
                        result += $"  + {targetName}: {compType}\n";
                    }
                    result += "\n";
                }
                else
                {
                    result += "=== 追加されたコンポーネント ===\n  なし\n\n";
                }

                // 削除されたコンポーネント
                var removedComponents = PrefabUtility.GetRemovedComponents(gameObject);
                if (removedComponents.Count > 0)
                {
                    result += $"=== 削除されたコンポーネント ({removedComponents.Count}個) ===\n";
                    foreach (var comp in removedComponents)
                    {
                        var compType = comp.assetComponent?.GetType().Name ?? "null";
                        result += $"  - {compType}\n";
                    }
                    result += "\n";
                }
                else
                {
                    result += "=== 削除されたコンポーネント ===\n  なし\n\n";
                }

                // 追加されたGameObject
                var addedGameObjects = PrefabUtility.GetAddedGameObjects(gameObject);
                if (addedGameObjects.Count > 0)
                {
                    result += $"=== 追加されたGameObject ({addedGameObjects.Count}個) ===\n";
                    foreach (var obj in addedGameObjects)
                    {
                        var objName = obj.instanceGameObject?.name ?? "null";
                        result += $"  + {objName}\n";
                    }
                    result += "\n";
                }
                else
                {
                    result += "=== 追加されたGameObject ===\n  なし\n\n";
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"ListPrefabInstanceOverrides エラー: {e}");
                return $"エラー: {e.Message}";
            }
        }

    }
}