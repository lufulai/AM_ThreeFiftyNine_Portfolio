using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Drop0", menuName = "Stage/Drop0")]
public class Drop0 : BossDebuffSO
{
    private bool isSet = false;

    public override void ResetDebuff()
    {
        isSet = false;
    }

    public override void InitDebuff()
    {
        if (!isSet)
        {
            isSet = true;
            CombatManager.instance.remaningDrops = 0;

            FieldUIManager.Instance.UpdateFieldUI(CombatManager.instance.remaningHands, CombatManager.instance.remaningDrops);

            GameLog.Info($"드랍 횟수 저주 적용:  {CombatManager.instance.remaningDrops}");
        }
    }

    public override void OnDebuff(List<CardController> selectedCards, ref long chip, ref double multiplier)
    {
    }
}
