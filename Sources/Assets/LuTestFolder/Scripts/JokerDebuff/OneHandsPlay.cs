using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "OneHands Play", menuName = "Stage/OneHands Play")]
public class OneHandsPlay : BossDebuffSO
{
    private int originalMaxHands;
    private bool hasOriginalMaxHands;

    public override void ResetDebuff()
    {
        if (CombatManager.instance != null && hasOriginalMaxHands)
        {
            // 저주 해제 시 원래대로 복구 (필요 시)
            CombatManager.instance.maxHands = originalMaxHands;
        }

        originalMaxHands = 0;
        hasOriginalMaxHands = false;
    }

    public override void InitDebuff()
    {
        if (CombatManager.instance != null)
        {
            // 원래 핸드 횟수 기억
            originalMaxHands = CombatManager.instance.maxHands;
            hasOriginalMaxHands = true;
            
            // 핸드 횟수를 무조건 1로 고정
            CombatManager.instance.maxHands = 1;
            CombatManager.instance.remaningHands = 1;
            long baseTargetScore = CombatManager.instance.ApplyCurrentStageBaseTargetScore();
            
            Debug.Log($"[Boss] OneHandsPlay 발동: 핸드 횟수가 1회로 제한되고 목표 점수가 스테이지 기본값({baseTargetScore})으로 설정됩니다!");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {

    }
}
