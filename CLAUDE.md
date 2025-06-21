# Unity Natural MCP Extension Tools

This package provides custom MCP (Model Context Protocol) tools that extend Unity Natural MCP server capabilities, enabling comprehensive automation of Unity Editor operations.

## パッケージ概要

Unity Natural MCP サーバーの機能を拡張し、Unityエディタ操作の包括的な自動化を実現するカスタムMCPツール群です。

## ツール構成

### 実装済みツール

| ツール名 | 責務 | 主要メソッド |
|---------|------|-------------|
| **McpSceneObjectTool** | シーンオブジェクト基本操作 | AddEmptyGameObject, AddPrimitiveToScene, DuplicateGameObject, DeleteObject, SetParentChild, SetTransformProperties |
| **McpMaterialTool** | マテリアル作成・設定・割り当て | CreateMaterial, SetMaterialColor, SetMaterialFloat, SetMaterialEmission, SetMaterialShader, ListMaterials, AssignMaterialToRenderer |
| **McpComponentPropertyTool** | コンポーネント詳細プロパティ設定 | SetRendererProperties, SetColliderProperties, SetAudioSourceProperties, AddComponentToObject, GetObjectInfo |
| **McpParticleTool** | パーティクルシステム操作 | CreateParticleSystem, SetParticleSystemMain, SetParticleSystemShape, SetParticleSystemEmission, SetParticleSystemVelocity, PlayParticleSystem, StopParticleSystem |
| **McpAssetTool** | アセット・Prefab・フォルダ管理 | CreateFolder, CreatePrefabFromGameObject, InstantiatePrefab, ListPrefabs, DeleteAsset, RefreshAssets |

### ディレクトリ構造

```
com.sack-kazu.unity-natural-mcp-extension-tools/
├── Editor/                     # MCPツール実装
│   ├── Editor.asmdef          # エディタアセンブリ定義
│   ├── McpSceneObjectTool.cs  # シーンオブジェクト操作
│   ├── McpSceneObjectToolBuilder.cs
│   ├── McpMaterialTool.cs     # マテリアル操作
│   ├── McpMaterialToolBuilder.cs
│   ├── McpComponentPropertyTool.cs  # コンポーネント設定
│   ├── McpComponentPropertyToolBuilder.cs
│   ├── McpParticleTool.cs     # パーティクルシステム
│   ├── McpParticleToolBuilder.cs
│   ├── McpAssetTool.cs        # アセット管理
│   └── McpAssetToolBuilder.cs
├── Runtime/                    # ScriptableObjectアセット
│   ├── McpSceneObjectToolBuilder.asset
│   ├── McpMaterialToolBuilder.asset
│   ├── McpComponentPropertyToolBuilder.asset
│   ├── McpParticleToolBuilder.asset
│   └── McpAssetToolBuilder.asset
└── package.json               # パッケージマニフェスト
```

## 実装パターン

### MCPツールクラスの基本構造

```csharp
[McpServerToolType, Description("ツールの説明")]
internal sealed class McpXxxTool
{
    [McpServerTool, Description("メソッドの説明")]
    public async ValueTask<string> MethodName(
        [Description("パラメータ説明")] string param1,
        [Description("オプションパラメータ")] string param2 = null)
    {
        try
        {
            await UniTask.SwitchToMainThread();
            // Unity Editor API 操作
            EditorUtility.SetDirty(target);
            return "成功メッセージ";
        }
        catch (Exception e)
        {
            Debug.LogError($"エラーログ: {e}");
            return $"エラー: {e.Message}";
        }
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

## 使用方法

### 1. パッケージのインストール

Unity Package Manager を使用してこのパッケージをインストールします。

### 2. ツールの自動登録

Unity Natural MCP サーバー起動時に、Runtime フォルダ内の ScriptableObject が自動的に読み込まれ、対応するツールがMCPサーバーに登録されます。

### 3. MCPツールの使用例

```python
# オブジェクト作成
await mcp__unity-natural-mcp__AddPrimitiveToScene(
    primitiveType="Cube",
    objectName="MyCube",
    position=[0, 1, 0]
)

# マテリアル作成と適用
await mcp__unity-natural-mcp__CreateMaterial(
    materialName="MyMaterial",
    shaderName="Universal Render Pipeline/Lit"
)

await mcp__unity-natural-mcp__SetMaterialColor(
    materialName="MyMaterial",
    propertyName="_BaseColor",
    colorValues=[1, 0, 0, 1]  # 赤色
)

await mcp__unity-natural-mcp__AssignMaterialToRenderer(
    objectName="MyCube",
    materialName="MyMaterial"
)
```

## 技術的詳細

### 非同期処理

- UniTask を使用してメインスレッドとの同期を管理
- `await UniTask.SwitchToMainThread()` で Unity API へのアクセスを保証

### エラーハンドリング

- 各メソッドは try-catch でエラーを捕捉
- エラーは Debug.LogError でログ出力
- ユーザーにはエラーメッセージを返却

### パフォーマンス考慮事項

- EditorUtility.SetDirty() で変更を適切にマーク
- 大量のオブジェクト操作時は適宜 AssetDatabase.Refresh() を呼び出し

## 拡張方法

新しいMCPツールを追加する場合：

1. Editor フォルダに新しいツールクラスを作成
2. 対応するツールビルダークラスを作成
3. Runtime フォルダにツールビルダーの ScriptableObject アセットを作成
4. Unity エディタを再起動してツールを登録

## ライセンス

[プロジェクトのライセンスに準拠]

## 貢献方法

[貢献ガイドラインへのリンク]