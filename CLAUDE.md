# Unity Natural MCP Extension Tools

This package provides unified custom MCP (Model Context Protocol) tools that extend Unity Natural MCP server capabilities, enabling comprehensive automation of Unity Editor operations with consolidated functionality.

## パッケージ概要

Unity Natural MCP サーバーの機能を拡張し、Unityエディタ操作の包括的な自動化を実現する統合型カスタムMCPツール群です。バージョン0.8.0では、Scene管理、Prefab編集モード、スクリーンショット機能を追加し、包括的な開発支援を実現しています。

## ツール構成

### 実装済みツール

| ツール名 | 責務 | 主要メソッド | メソッド数 |
|---------|------|-------------|-----------|
| **McpUnifiedObjectTool** | オブジェクト作成・操作・プロパティ設定 | CreateObject, ManipulateObject, ConfigureComponent, GetObjectInfo, ListSceneObjects | 5 |
| **McpUnifiedAssetTool** | マテリアル・アセット・フォルダ管理 | ManageMaterial, AssignMaterialToRenderer, ListMaterials, ManageAsset, ListPrefabs | 5 |
| **McpUnifiedEffectTool** | パーティクルシステム管理 | ConfigureParticleSystem, ControlParticleSystem | 2 |
| **McpSceneCaptureTool** | シーンスクリーンショット機能 | CaptureScene, CaptureGameView, ListCapturedScreenshots | 3 |
| **McpPrefabEditTool** | Prefab編集モード管理 | OpenPrefabMode, SavePrefabChanges, ExitPrefabMode, GetPrefabEditStatus | 4 |
| **McpSceneManagementTool** | Scene作成・管理・操作 | CreateScene, SaveScene, LoadScene, ListScenes, GetActiveSceneInfo, CloseScene | 6 |
| **McpProjectSettingsTool** | プロジェクト設定管理 | ManageProjectLayers | 1 |
| **合計** | **全領域カバー** | **全26メソッド** | **26** |

### ディレクトリ構造

```
com.sack-kazu.unity-natural-mcp-extension-tools/
├── Editor/                     # MCPツール実装
│   ├── Editor.asmdef          # エディタアセンブリ定義
│   ├── McpUnifiedObjectTool.cs # 統合オブジェクト操作
│   ├── McpUnifiedObjectToolBuilder.cs
│   ├── McpUnifiedAssetTool.cs  # 統合アセット・マテリアル操作
│   ├── McpUnifiedAssetToolBuilder.cs
│   ├── McpUnifiedEffectTool.cs # 統合エフェクトシステム
│   ├── McpUnifiedEffectToolBuilder.cs
│   ├── McpSceneCaptureTool.cs  # シーンキャプチャ機能
│   ├── McpSceneCaptureToolBuilder.cs
│   ├── McpPrefabEditTool.cs    # Prefab編集モード管理
│   ├── McpPrefabEditToolBuilder.cs
│   ├── McpSceneManagementTool.cs # Scene作成・管理機能
│   ├── McpSceneManagementToolBuilder.cs
│   ├── McpProjectSettingsTool.cs # プロジェクト設定管理
│   ├── McpProjectSettingsToolBuilder.cs
│   ├── McpToolBase.cs          # 共通基盤クラス (v0.8.1新規)
│   ├── McpToolUtilities.cs      # 共通ユーティリティ (v0.8.1拡張)
│   └── ComponentPropertyManager.cs # 型変換・プロパティ設定管理 (v0.8.2新規)
├── Runtime/                    # ScriptableObjectアセット
│   ├── McpUnifiedObjectToolBuilder.asset
│   ├── McpUnifiedAssetToolBuilder.asset
│   ├── McpUnifiedEffectToolBuilder.asset
│   ├── McpSceneCaptureToolBuilder.asset
│   ├── McpPrefabEditToolBuilder.asset
│   ├── McpSceneManagementToolBuilder.asset
│   └── McpProjectSettingsToolBuilder.asset
└── package.json               # パッケージマニフェスト
```

## 実装パターン

### MCPツールクラスの基本構造 (v0.8.1更新)

```csharp
[McpServerToolType, Description("ツールの説明")]
internal sealed class McpXxxTool : McpToolBase  // v0.8.1: McpToolBaseを継承
{
    [McpServerTool, Description("メソッドの説明")]
    public async ValueTask<string> MethodName(
        [Description("パラメータ説明")] string param1,
        [Description("オプションパラメータ")] string param2 = null)
    {
        // v0.8.1: ExecuteOperationによる統一されたエラーハンドリング
        return await ExecuteOperation(async () =>
        {
            // Prefabモード検証が必要な場合
            await ValidatePrefabMode(inPrefabMode);
            
            // GameObjectの安全な検索
            var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
            
            // Unity Editor API 操作
            EditorUtility.SetDirty(target);
            MarkSceneDirty(inPrefabMode);
            
            // 標準化されたメッセージ生成
            return McpToolUtilities.CreateSuccessMessage($"操作が完了しました");
        }, "operation name");
    }
}
```

### ツールビルダークラスの構造

```csharp
[CreateAssetMenu(menuName = "MCP/Tool Builder/Xxx", fileName = "McpXxxToolBuilder")]
public class McpXxxToolBuilder : McpBuilderScriptableObject
{
    protected override Type McpType => typeof(McpXxxTool);
}
```

## セットアップ

Unity Package Manager を使用してこのパッケージをインストールします。Unity Natural MCP サーバー起動時に、Runtime フォルダ内の ScriptableObject が自動的に読み込まれ、対応するツールがMCPサーバーに登録されます。

## 技術的詳細

### 共通基盤クラス (v0.8.1新規)

#### McpToolBase
提供される共通機能：
- `ExecuteOperation`: 標準化されたエラーハンドリング付き操作実行
- `ValidatePrefabMode`: Prefabモード検証
- `MarkSceneDirty`: コンテキストに応じたシーンのダーティマーク
- `FindGameObjectSafe`: 安全なGameObject検索
- `GetCurrentPrefabStage/GetContextRoot`: コンテキスト情報取得
- `LogSuccess/LogWarning`: 統一されたログ出力

#### McpToolUtilities (v0.8.1拡張)
追加された機能：
- `CreateSuccessMessage/CreateErrorMessage`: 標準化されたメッセージ生成
- `GetContextDescription`: コンテキスト説明文生成
- `ValidateContext`: コンテキスト検証
- `FindComponent<T>`: ジェネリックコンポーネント検索

#### ComponentPropertyManager (v0.8.2新規)
型変換とプロパティ設定を統一管理：
- `SetProperty`: コンポーネントのプロパティ/フィールドを自動的に検出して設定
- `SetNestedProperty`: ドット記法によるネストされたプロパティの設定（例: "material.color"）
- `ResolveComponentType`: コンポーネント型の解決とキャッシュ機能
- `GetComponentSuggestions`: 類似したコンポーネント名の提案（Levenshtein距離による）
- **自動型変換**: Unity特有の型に対応
  - Vector2/3/4、Color、Quaternion
  - LayerMask（文字列または数値から）
  - Unity Object参照（GameObject、Transform、Component）
  - Enum型の文字列/数値変換
  - JToken/JArrayからの自動変換

### 非同期処理

- UniTask を使用してメインスレッドとの同期を管理
- `await UniTask.SwitchToMainThread()` で Unity API へのアクセスを保証

### エラーハンドリング (v0.8.1更新)

- **統一されたエラーハンドリング**: `McpToolBase.ExecuteOperation`メソッドによる一元管理
- **標準化されたメッセージ生成**: `McpToolUtilities.CreateSuccessMessage/CreateErrorMessage`
- **一貫性のあるログ出力**: `McpToolBase.LogSuccess/LogWarning`メソッド
- **コンテキスト認識**: Prefabモード/シーンモードを自動識別してメッセージに反映

### パフォーマンス考慮事項

- EditorUtility.SetDirty() で変更を適切にマーク
- 大量のオブジェクト操作時は適宜 AssetDatabase.Refresh() を呼び出し

## 拡張方法

新しいMCPツールを追加する場合：

1. Editor フォルダに新しいツールクラスを作成（**v0.8.1**: `McpToolBase`を継承）
2. 対応するツールビルダークラスを作成
3. Runtime フォルダにツールビルダーの ScriptableObject アセットを作成
4. Unity エディタを再起動してツールを登録

### v0.8.2 推奨実装パターン

```csharp
[McpServerToolType, Description("新しいツールの説明")]
internal sealed class McpNewTool : McpToolBase
{
    [McpServerTool, Description("メソッドの説明")]
    public async ValueTask<string> NewMethod(
        [Description("パラメータ")] string param,
        [Description("Prefabモードで実行")] bool inPrefabMode = false)
    {
        return await ExecuteOperation(async () =>
        {
            // 必要に応じてPrefabモード検証
            await ValidatePrefabMode(inPrefabMode);
            
            // GameObject検索
            var obj = await FindGameObjectSafe(param, inPrefabMode);
            
            // 処理実装
            // ...
            
            // シーンのダーティマーク
            MarkSceneDirty(inPrefabMode);
            
            // 成功メッセージ
            return McpToolUtilities.CreateSuccessMessage("操作が完了しました");
        }, "new operation");
    }
}
```

#### コンポーネントプロパティ設定の例 (v0.8.2)

```csharp
// ConfigureComponentメソッドの使用例
public async ValueTask<string> ConfigureComponent(
    string objectName,
    string componentType,
    string properties = null,
    bool inPrefabMode = false)
{
    return await ExecuteOperation(async () =>
    {
        var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
        var compType = ComponentPropertyManager.ResolveComponentType(componentType);
        
        if (compType == null)
        {
            var suggestions = ComponentPropertyManager.GetComponentSuggestions(componentType);
            var suggestionText = suggestions.Any() ? $" Did you mean: {string.Join(", ", suggestions)}?" : "";
            return $"Error: Component type '{componentType}' not found.{suggestionText}";
        }
        
        // プロパティ設定
        if (!string.IsNullOrEmpty(properties))
        {
            var propsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(properties);
            foreach (var prop in propsDict)
            {
                var result = prop.Key.Contains(".")
                    ? ComponentPropertyManager.SetNestedProperty(component, prop.Key, prop.Value, inPrefabMode)
                    : ComponentPropertyManager.SetProperty(component, prop.Key, prop.Value, inPrefabMode);
                
                // エラーハンドリング
                if (!result.Success)
                {
                    errors.Add($"{prop.Key}: {result.ErrorMessage}");
                }
            }
        }
        
        return McpToolUtilities.CreateSuccessMessage("Component configured", objectName);
    }, "ConfigureComponent", inPrefabMode);
}
```
