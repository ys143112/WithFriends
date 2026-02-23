using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class ClassSelectUIController : MonoBehaviour
{
    [Header("Data")]
    public ClassDatabase classDatabase;

    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text statText;
    public TMP_Text descText;   // ✅ 직업 설명 UI 추가

    private JobType current = JobType.Warrior;

    private bool pendingSend;   // 아직 못 보낸 선택이 있다
    private bool sending;       // 코루틴 중복 방지

    void Awake()
    {
        Debug.Log("[UI] ClassSelectUIController Awake");
    }

    void Start()
    {
        Debug.Log("[UI] ClassSelectUIController Start");

        // ✅ 시작값도 Select로 통일(프리뷰/캐시/전송대기까지 한 번에)
        Select(current);
    }

    public void ClickWarrior() => Select(JobType.Warrior);

    public void ClickArcher()
    {
        Debug.Log("[UI] ClickArcher pressed");
        Select(JobType.Archer);
    }

    public void ClickHealer() => Select(JobType.Healer);

    void Select(JobType job)
    {
        current = job;

        // ✅ 로컬 선택 저장 (GameScene에서 스폰 시 적용됨)
        SelectedJobCache.Selected = job;

        // ✅ UI 미리보기(이름/스탯/설명)
        Preview(job);

        // ✅ 네트워크 준비되면 서버로도 반영 시도(가능한 경우)
        QueueSend(job);
    }

    void Preview(JobType job)
    {
        var def = classDatabase.Get(job);
        if (def == null) return;

        if (nameText) nameText.text = def.displayName;

        if (statText)
        {
            statText.text =
                $"HP: {def.baseHp}\n" +
                $"ATK: {def.baseAtk}\n" +
                $"SPD: {def.moveSpeed}";
        }

        // ✅ 직업 설명 출력 (ClassDefinition에 description 필드 필요)
        if (descText)
            descText.text = def.description;
    }

    void QueueSend(JobType job)
    {
        pendingSend = true;

        if (!sending)
            StartCoroutine(CoTrySendWhenReady());
    }

    IEnumerator CoTrySendWhenReady()
    {
        sending = true;

        // 네트워크 준비 대기
        while (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient)
            yield return null;

        // PlayerObject 스폰 대기
        while (NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
            yield return null;

        // PlayerClassState 스폰 대기 (IsSpawned까지)
        while (true)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            var state = playerObj.GetComponent<PlayerClassState>();

            if (pendingSend && state != null && state.NetworkObject != null && state.NetworkObject.IsSpawned)
            {
                pendingSend = false;

                Debug.Log($"[UI] Sending job={(int)current} to PlayerClassState. " +
                          $"playerObj={playerObj.name}, state=OK");

                state.RequestSetJobRpc((int)current);
                break;
            }

            // 아직 못 보내는 상태면 계속 대기
            yield return null;
        }

        sending = false;
    }
}
