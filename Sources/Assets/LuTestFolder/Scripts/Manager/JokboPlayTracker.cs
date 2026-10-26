using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JokboPlayTracker : MonoBehaviour
{
    public static JokboPlayTracker Instance { get; private set; }

    // 단위별로 족보 플레이 횟수를 저장하는 딕셔너리
    public Dictionary<JokBoType, int> TotalPlayCount = new Dictionary<JokBoType, int>();
    public Dictionary<JokBoType, int> StagePlayCount = new Dictionary<JokBoType, int>();
    public Dictionary<JokBoType, int> RoundPlayCount = new Dictionary<JokBoType, int>();

    public JokBoType CurrentEvaluatingJokbo = JokBoType.None;
    private List<JokBoType> roundPlayHistory = new List<JokBoType>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 새로운 게임 시작 시 전체 리셋
    public void ResetAll()
    {
        TotalPlayCount.Clear();
        StagePlayCount.Clear();
        RoundPlayCount.Clear();
        roundPlayHistory.Clear();
        CurrentEvaluatingJokbo = JokBoType.None;
    }

    /// <summary>JSON이 지원하지 않는 Dictionary를 저장 가능한 목록으로 변환합니다.</summary>
    public List<RunJokboCountEntry> CaptureTotalPlayCounts()
    {
        List<RunJokboCountEntry> entries = new List<RunJokboCountEntry>();
        foreach (KeyValuePair<JokBoType, int> pair in TotalPlayCount)
        {
            if (pair.Key == JokBoType.None || pair.Value <= 0)
                continue;

            entries.Add(new RunJokboCountEntry
            {
                jokboType = pair.Key,
                count = pair.Value
            });
        }

        return entries;
    }

    /// <summary>이어하기는 노드 진입 전부터 시작하므로 전체 횟수만 복원하고 관·라운드 횟수는 비웁니다.</summary>
    public void RestoreTotalPlayCounts(List<RunJokboCountEntry> entries)
    {
        ResetAll();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            RunJokboCountEntry entry = entries[i];
            if (entry == null || entry.jokboType == JokBoType.None || entry.count <= 0)
                continue;

            TotalPlayCount[entry.jokboType] = entry.count;
        }
    }

    // 스테이지 시작 시 스테이지 카운트 리셋
    public void ResetForNewStage()
    {
        StagePlayCount.Clear();
        RoundPlayCount.Clear();
        roundPlayHistory.Clear();
        CurrentEvaluatingJokbo = JokBoType.None;
    }

    // 라운드 시작 시 라운드 카운트 리셋
    public void ResetForNewRound()
    {
        RoundPlayCount.Clear();
        roundPlayHistory.Clear();
        CurrentEvaluatingJokbo = JokBoType.None;
    }

    // 카드를 제출했을 때 (CombatManager의 OnPlayCards 등에서 호출)
    public void RecordJokboPlay(JokBoType playedJokbo)
    {
        if (playedJokbo == JokBoType.None) return;

        AddCount(TotalPlayCount, playedJokbo);
        AddCount(StagePlayCount, playedJokbo);
        AddCount(RoundPlayCount, playedJokbo);

        roundPlayHistory.Add(playedJokbo);
        SaveManager.instance?.SaveJokboPlayStatistics(CaptureTotalPlayCounts());
    }

    private void AddCount(Dictionary<JokBoType, int> dict, JokBoType type)
    {
        if (dict.ContainsKey(type)) dict[type]++;
        else dict[type] = 1;
    }

    // 지금까지 전체 게임에서 가장 많이 플레이한 족보 반환 (2번 보스를 위해 필요)
    public JokBoType GetMostPlayedJokboTotal()
    {
        if (TotalPlayCount.Count == 0) return JokBoType.None;
        
        // Value(횟수)를 기준으로 내림차순 정렬 후 가장 첫 번째 Key 반환
        return TotalPlayCount.OrderByDescending(x => x.Value).First().Key;
    }

    public JokBoType GetFirstPlayedJokboThisRound()
    {
        if (roundPlayHistory.Count > 0) return roundPlayHistory[0];
        return JokBoType.None;
    }
}
