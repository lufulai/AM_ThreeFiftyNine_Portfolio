using UnityEngine;
using TMPro;

public class JokboRowUI : MonoBehaviour
{
    [Header("이 줄이 담당할 족보")]
    public JokBoType targetJokbo;

    [Header("UI 텍스트 연결")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI chipText;
    public TextMeshProUGUI multText;
    public TextMeshProUGUI countText;

    // 패널이 켜질 때마다 최신 정보로 갱신하는 함수
    public void RefreshData()
    {
        JokboRuleProfile ruleProfile = ScoreManager.Instance != null
            ? ScoreManager.Instance.ActiveJokboRuleProfile
            : JokboRuleProfile.LoadResourcesDefaultOrRuntime();

        if (nameText != null)
            nameText.text = ruleProfile.GetDisplayName(targetJokbo);

        GetBaseStats(ruleProfile, targetJokbo, out int baseChip, out int baseMult);
        // 2. 레벨, 칩, 배수 가져오기 (PlanetCardLevelManager 활용)
        int currentLevel = 1;
        int currentChip = baseChip;
        int currentMult = baseMult; 

        if (PlanetCardLevelManager.Instance != null)
        {
            if (PlanetCardLevelManager.Instance.jokboLevel.TryGetValue(targetJokbo, out PlanetStats stats))
            {
                currentLevel = stats.level;
                currentChip += stats.plusChip;
                currentMult += stats.plusMulti;
            }
        }

        if (levelText != null) levelText.text = $"Lv.{currentLevel}";
        if (chipText != null) chipText.text = currentChip.ToString();
        if (multText != null) multText.text = $"×{currentMult}";

        // 3. 사용 횟수 가져오기 (JokboPlayTracker 활용)
        int playCount = 0;
        if (JokboPlayTracker.Instance != null)
        {
            if (JokboPlayTracker.Instance.TotalPlayCount.TryGetValue(targetJokbo, out int count))
            {
                playCount = count;
            }
        }
        
        if (countText != null) countText.text = GameText.Get("game.JokboRowUI.RefreshData.791de22adc", "{value0}회", new { value0 = playCount });
    }

    private static void GetBaseStats(
        JokboRuleProfile ruleProfile,
        JokBoType type,
        out int chip,
        out int mult)
    {
        JokboRuleDefinition rule = ruleProfile != null ? ruleProfile.FindRule(type) : null;
        if (rule != null)
        {
            chip = Mathf.Max(0, rule.bonusChip);
            mult = Mathf.Max(1, rule.multiplier);
            return;
        }

        GetLegacyBaseStats(type, out chip, out mult);
    }

    private static void GetLegacyBaseStats(JokBoType type, out int chip, out int mult)
    {
        switch (type)
        {
            case JokBoType.ThreeGwang138: chip = 100; mult = 10; break; // 예시 수치입니다.
            case JokBoType.ThreeGwangAny: chip = 85; mult = 8; break;
            case JokBoType.SamDong:       chip = 75; mult = 7; break;
            case JokBoType.Yang:          chip = 55; mult = 5; break;
            case JokBoType.Eum:           chip = 65; mult = 6; break;
            case JokBoType.TwoGwang:      chip = 50; mult = 5; break;
            case JokBoType.BbingPae:      chip = 45; mult = 5; break;
            case JokBoType.SaPae:         chip = 40; mult = 4; break;
            case JokBoType.JangDdaeng:    chip = 30; mult = 3; break;
            case JokBoType.Ddaeng:        chip = 20; mult = 3; break;
            case JokBoType.Mangtong:      chip = 10; mult = 2; break;
            case JokBoType.Ggeut:         chip = 5;  mult = 1; break;
            default:                      chip = 0;  mult = 1; break;
        }
    }
}
