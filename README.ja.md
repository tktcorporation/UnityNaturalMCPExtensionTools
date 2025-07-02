# Unity Natural MCP Extension Tools

[English](README.md)

[Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP)サーバーの機能を拡張し、包括的なUnity Editor自動化を実現するカスタムMCP（Model Context Protocol）ツール群です。

> [!WARNING]
> これは自分のために制作中のもので、動作は保証できません

**このリポジトリの大部分はClaudeCodeによって作成されています**

## ベースプロジェクト

これは、notargs氏による[Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP)の拡張ツールです。

**Unity Natural MCPがインストールされていないと機能しません**

## 機能

### 🎯 統合オブジェクト管理 (McpUnifiedObjectTool)
- 空のGameObject、プリミティブ、Prefabインスタンスの作成
- オブジェクトの変形、複製、削除、親子関係設定
- コンポーネントの追加と設定（JSON構造化設定対応）
- GameObjectの詳細情報取得
- シーン内オブジェクトの一覧表示とフィルタリング

### 🎨 統合アセット・マテリアル管理 (McpUnifiedAssetTool)
- マテリアルの作成・更新（JSON構造化設定対応）
- レンダラーへのマテリアル割り当て
- マテリアルの一覧表示とフィルタリング
- フォルダ作成、Prefab作成、アセット削除
- Prefabの一覧表示と詳細情報取得

### ✨ 統合エフェクトシステム (McpUnifiedEffectTool)
- パーティクルシステムの包括的設定（JSON構造化設定対応）
- パーティクルシステムの再生・停止制御
- パーティクルシステムの詳細情報取得

### 📷 シーンキャプチャ機能 (McpSceneCaptureTool)
- シーンビューからのスクリーンショット撮影
- ゲームビューからのスクリーンショット撮影
- Prefab編集モードでのPrefabキャプチャ
- キャプチャしたスクリーンショットの一覧管理

### 📦 Prefab編集モード管理 (McpPrefabEditTool)
- Prefab編集モードの開始・保存・終了
- Prefab編集状態の確認
- Prefabインスタンスの変更をソースPrefabに適用
- Prefabインスタンスの変更をソース状態に復元
- Prefabインスタンスの変更情報と一覧表示

### 🏗️ Scene管理機能 (McpSceneManagementTool)
- 新しいシーンの作成（Empty、3D、2D、UIテンプレート対応）
- シーンの保存と名前付け保存
- シーンの読み込み（Single、Additiveモード対応）
- プロジェクト内シーンの一覧表示
- アクティブシーンの詳細情報取得
- シーンのクローズ（マルチシーン対応）

### ⚙️ プロジェクト設定管理 (McpProjectSettingsTool)
- プロジェクトレイヤーの管理（一覧表示、名前設定、削除）

### 🔧 共通基盤システム
- 統一されたエラーハンドリングとログ出力
- Prefabモード/シーンモード自動識別
- 型安全なJSON設定の解析と検証
- Unity特有型の自動変換（Vector、Color、Quaternion等）
- ネストされたプロパティ設定（ドット記法対応）

## 動作要件

[Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP/tree/main?tab=readme-ov-file#requirements)に準じます

## インストール

1. [Unity Natural MCP](https://github.com/notargs/UnityNaturalMCP)がインストールされ、設定されていることを確認してください
2. Package Manager経由でこのパッケージをUnityプロジェクトに追加してください
   
  ```
  https://github.com/sack-kazu/UnityNaturalMCPExtensionTools.git
  ```

3. MCPサーバーが起動すると、ツールが自動的に登録されます

## ライセンス

MIT License - このプロジェクトは、同じくMITライセンスのUnity Natural MCPを拡張しています。


## 謝辞

- Unity Natural MCPを作成した[notargs](https://github.com/notargs)氏
- Unity Editor APIを提供するUnity Technologies
- Model Context Protocolを開発したAnthropic