using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class HadoPlayerState : UdonSharpBehaviour
{
    [Header("--- チーム & プレイヤー識別 ---")]
    [UdonSynced] public int teamId = -1; // 0: Aチーム, 1: Bチーム

    [Header("--- ライフ & 生死状態 ---")]
    [UdonSynced] public int currentLife = 4;
    [UdonSynced] public bool isDead = false;

    [Header("--- カスタムステータスポイント (合計6P / 各最大5P) ---")]
    public int bulletSpeedPoint = 0;
    public int bulletSizePoint = 0;
    public int maxAmmoPoint = 0;
    public int shieldStrengthPoint = 0;

    [Header("--- パラメーターから計算されるゲーム実数値 ---")]
    [HideInInspector] public float bulletSpeed = 10f;
    [HideInInspector] public float bulletSize = 1f;
    [HideInInspector] public int maxAmmo = 2;
    [HideInInspector] public int ammoPerCore = 1;
    [HideInInspector] public float maxShieldHealth = 100f;

    [Header("--- 試合中の動的ステータス ---")]
    public int currentAmmo = 0;
    [UdonSynced] public bool hasShieldLeft = true; // 1試合1回のみ使用可能

    private VRCPlayerApi localPlayer;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        InitializeStats();
    }

    /// <summary>
    /// カスタムポイントを基に、実際のゲーム用実数値を計算して適用する
    /// </summary>
    public void InitializeStats()
    {
        // 1. 弾速の計算 (基本値 10m/s + 1Pにつき 2m/s 上昇)
        bulletSpeed = 10f + (bulletSpeedPoint * 2f);

        // 2. 弾のサイズの計算 (基本値 1.0倍 + 1Pにつき 0.2倍 拡大)
        bulletSize = 1f + (bulletSizePoint * 0.2f);

        // 3. 最大装弾数・コア取得時のリロード量の計算 (仕様書準拠)
        if (maxAmmoPoint >= 5)
        {
            maxAmmo = 5;
            ammoPerCore = 3;
        }
        else if (maxAmmoPoint >= 3)
        {
            maxAmmo = 4;
            ammoPerCore = 2;
        }
        else
        {
            maxAmmo = 2;
            ammoPerCore = 1;
        }

        // 4. シールド耐久値の計算 (基本値 100 + 1Pにつき 30 上昇)
        maxShieldHealth = 100f + (shieldStrengthPoint * 30f);
    }

    /// <summary>
    /// 自陣のリスポーン位置を設定する (GameManager等から呼び出し)
    /// </summary>
    public void SetSpawnPoint(Vector3 pos, Quaternion rot)
    {
        spawnPosition = pos;
        spawnRotation = rot;
    }

    /// <summary>
    /// エネルギーコア取得時のリロード処理 (HadoEnergyCoreから呼ばれる)
    /// </summary>
    public void AddAmmoFromCore()
    {
        if (isDead) return;

        // ステータスに応じたリロード量を加算
        currentAmmo += ammoPerCore;
        if (currentAmmo > maxAmmo)
        {
            currentAmmo = maxAmmo;
        }

        // TODO: UIスクリプトへ弾数更新を通知する処理
    }

    /// <summary>
    /// 被弾時のダメージ処理 (食らい手ローカル最優先判定でHadoBallから呼ばれる)
    /// </summary>
    public void TakeDamage()
    {
        if (isDead) return;

        // 所有権が自分にない場合は取得する（同期エラー防止）
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(localPlayer, gameObject);
        }

        currentLife--;

        if (currentLife <= 0)
        {
            currentLife = 0;
            isDead = true;
            currentAmmo = 0; // ダウン時は弾数をリセット
            
            // 全プレイヤーへ状態を同期
            RequestSerialization();

            // TODO: GameManagerへKOを通知し、敵チームに加点する処理をここに記述
            // gameManager.AddScore(teamId == 0 ? 1 : 0); 

            // 3秒後に自動リスポーン
            SendCustomEventDelayedSeconds(nameof(Respawn), 3.0f);
        }
        else
        {
            RequestSerialization();
        }
    }

    /// <summary>
    /// リスポーン（復活）処理
    /// </summary>
    public void Respawn()
    {
        if (!Networking.IsOwner(gameObject)) return;

        currentLife = 4;
        isDead = false;
        currentAmmo = 0; // 復活時は弾数0からスタート（コアの回収を促す）

        RequestSerialization();

        // 記憶している自陣の初期位置へテレポート
        if (localPlayer != null)
        {
            localPlayer.TeleportTo(spawnPosition, spawnRotation);
        }
    }
}