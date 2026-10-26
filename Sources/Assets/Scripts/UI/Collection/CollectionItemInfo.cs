using System;
using UnityEngine;

/// <summary>저장된 목록 카드의 원본/효과 키입니다. 원본 효과와 별도의 복제 설명을 저장하지 않습니다.</summary>
[Serializable]
public sealed class CollectionCatalogEntry
{
    public CollectionPanelView.Category category;
    public ScriptableObject source;
    public EnhancementCategory effectFamily;
    public int effectVariant;
    public CollectionEntryView view;
}

/// <summary>해금·발견 판정과 도감용 문구를 읽기만 하는 변환기입니다.</summary>
public static class CollectionItemInfo
{
    public static string Key(CollectionCatalogEntry entry)
    {
        if (entry.source is ArtifactData artifact) return "Artifact:" + artifact.artifactID;
        if (entry.source is BaseCardSO card) return CollectionDiscovery.Key(card);
        if (entry.source is VoucherData voucher) return CollectionDiscovery.Key(entry.category, voucher.voucherID);
        return entry.category == CollectionPanelView.Category.Edition
            ? CollectionDiscovery.Key(entry.category, ((EditionType)entry.effectVariant).ToString())
            : CollectionDiscovery.EffectKey(entry.effectFamily, entry.effectVariant);
    }

    public static bool IsUnlocked(CollectionCatalogEntry entry, UserData profile)
    {
        if (entry.source is ArtifactData artifact)
            return artifact.unlockType == UnlockConditionType.None
                || (profile?.unlockedItemIDs?.Contains(artifact.artifactID) ?? false);
        if (entry.source is JokerSO joker) return ProfileProgressCalculator.IsJokerUnlocked(joker, profile);
        if (entry.source is VoucherData voucher)
            return voucher.unlockType == UnlockConditionType.None
                || (profile?.unlockedItemIDs?.Contains(voucher.voucherID) ?? false)
                || (profile?.unlockedItemIDs?.Contains(voucher.voucherName) ?? false);
        return true;
    }

    public static bool IsDiscovered(CollectionCatalogEntry entry, UserData profile)
        => entry.source is ArtifactData artifact
            ? (profile?.discoveredArtifactIDs?.Contains(artifact.artifactID) ?? false)
            : CollectionDiscovery.Contains(profile, Key(entry));

    public static bool IsVisible(CollectionCatalogEntry entry, UserData profile)
        => IsUnlocked(entry, profile) && IsDiscovered(entry, profile);

    public static string Name(CollectionCatalogEntry entry)
    {
        if (entry.source is ArtifactData artifact) return GameContent.Get(artifact, "artifactName", artifact.artifactName);
        if (entry.source is JokerSO joker) return GameContent.Get(joker, "jokerName", joker.jokerName);
        if (entry.source is BaseCardSO card) return GameContent.Get(card, "cardName", card.cardName);
        if (entry.source is VoucherData voucher) return GameContent.Get(voucher, "voucherName", voucher.voucherName);
        return Effect(entry).displayName;
    }

    public static string Ability(CollectionCatalogEntry entry)
    {
        // 도감은 보유 인스턴스의 성장치를 조회하지 않고 원본의 효과만 보여 줍니다.
        if (entry.source is ArtifactData artifact) return Text(GameContent.Get(artifact, "artifactExplanation", artifact.artifactExplanation));
        if (entry.source is JokerSO joker) return Text(GameContent.Get(joker, "description", joker.description));
        if (entry.source is AmuletSO amulet) return ItemInfoTextBuilder.GetConsumableEffectSummary(amulet);
        if (entry.source is PlanetSO planet)
            return GameText.Get("game.CollectionItemInfo.Ability.5b44ece446", "{value0}\n\n족보 레벨 +1\n칩 +{value1} / 배수 +{value2}", new { value0 = GameContent.Get(planet, "amuletDescription", planet.amuletDescription), value1 = planet.plusChip, value2 = planet.plusMulti });
        if (entry.source is VoucherData voucher) return ItemInfoTextBuilder.BuildVoucherEffectSummary(voucher);
        return Text(Effect(entry).description);
    }

    // 강화·가호는 전투 툴팁의 설명을 재사용하고 원본 SO/런 상태를 변경하지 않습니다.
    private static CardTooltipEnhancementData Effect(CollectionCatalogEntry entry)
    {
        var descriptions = CardTooltipDataBuilder.BuildEffectDescriptions(
            entry.effectFamily == EnhancementCategory.Dan ? (DanEnhancement)entry.effectVariant : DanEnhancement.None,
            entry.effectFamily == EnhancementCategory.YulGgeut ? (YulGgeutEnhancement)entry.effectVariant : YulGgeutEnhancement.None,
            entry.effectFamily == EnhancementCategory.Gwang ? (GwangEnhancement)entry.effectVariant : GwangEnhancement.None,
            entry.category == CollectionPanelView.Category.Edition ? (EditionType)entry.effectVariant : EditionType.None);
        return descriptions.Count > 0 ? descriptions[0]
            : new CardTooltipEnhancementData { displayName = "?", description = "?" };
    }

    private static string Text(string value) => string.IsNullOrWhiteSpace(value) ? GameText.Get("game.CollectionItemInfo.Text.724f0574e5", "설명 미등록") : value;

    public static string Status(CollectionCatalogEntry entry, UserData profile)
        => !IsUnlocked(entry, profile) ? GameText.Get("game.CollectionItemInfo.Status.00b1cc99dc", "미해금") : !IsDiscovered(entry, profile) ? GameText.Get("game.CollectionItemInfo.Status.9a705a638b", "미발견") : GameText.Get("game.CollectionItemInfo.Status.ccd505afbd", "발견 완료");

    public static string Description(CollectionCatalogEntry entry, UserData profile)
    {
        string condition = UnlockCondition(entry);
        // 숨김 상태에서는 능력뿐 아니라 가격·등급·이야기·시너지 등의 부가 정보도 내보내지 않습니다.
        if (!IsVisible(entry, profile)) return GameText.Get("game.CollectionItemInfo.Description.3b8c678a4a", "능력: ?\n\n해금 조건\n{value0}", new { value0 = condition });
        string description = GameText.Get("game.CollectionItemInfo.Description.96e30f099a", "능력\n{value0}\n\n해금 조건\n{value1}", new { value0 = Ability(entry), value1 = condition });
        if (entry.source is ArtifactData artifact)
            description += GameText.Get("game.CollectionItemInfo.Description.ffc7c4d2f9", "\n\n등급: {value0}{value1}", new { value0 = ArtifactRarityDisplay.GetLabel(artifact.artifactType), value1 = (string.IsNullOrWhiteSpace(artifact.artifactDescription) ? "" : "\n\n" + GameText.Get("content.section.lore", "이야기") + "\n" + GameContent.Get(artifact, "artifactDescription", artifact.artifactDescription)) });
        return description;
    }

    public static string UnlockCondition(CollectionCatalogEntry entry)
    {
        if (entry.source is ArtifactData artifact) return Condition(artifact.unlockType, artifact.unlockValue);
        if (entry.source is VoucherData voucher) return Condition(voucher.unlockType, voucher.unlockValue);
        if (entry.source is JokerSO joker)
            return joker.unlockType == UnlockConditionType.None ? GameText.Get("game.CollectionItemInfo.UnlockCondition.f4e16419ec", "해금 조건 없음")
                : !string.IsNullOrWhiteSpace(joker.unlockDescription) ? GameContent.Get(joker, "unlockDescription", joker.unlockDescription)
                : joker.unlockType == UnlockConditionType.JourneyWinWithJoker
                    ? GameText.Get("game.CollectionItemInfo.UnlockCondition.c94f9a4f00", "탈 ID {value0}를 덱에 넣고 여정 승리", new { value0 = joker.unlockRequiredJokerID })
                : Condition(joker.unlockType, joker.unlockValue);
        return GameText.Get("game.CollectionItemInfo.UnlockCondition.f4e16419ec", "해금 조건 없음");
    }

    // 타이틀에는 전투 UnlockManager가 없을 수도 있으므로 조건 문구를 순수하게 변환합니다.
    public static string Condition(UnlockConditionType type, int value)
    {
        switch (type)
        {
            case UnlockConditionType.None: return GameText.Get("game.CollectionItemInfo.Condition.f4e16419ec", "해금 조건 없음");
            case UnlockConditionType.PlayCount: return GameText.Get("game.CollectionItemInfo.Condition.9e6f0d65d8", "플레이 {value0}회 달성", new { value0 = value });
            case UnlockConditionType.WinCount: return GameText.Get("game.CollectionItemInfo.Condition.666e959754", "승리 {value0}회 달성", new { value0 = value });
            case UnlockConditionType.HandPlayCount: return GameText.Get("game.CollectionItemInfo.Condition.3015fb3eb1", "패 제출 {value0}회 달성", new { value0 = value });
            case UnlockConditionType.ThrowCount: return GameText.Get("game.CollectionItemInfo.Condition.200e3c715a", "버리기 {value0}회 달성", new { value0 = value });
            case UnlockConditionType.MaxScore: return GameText.Get("game.CollectionItemInfo.Condition.90d55ec0f9", "최고 점수 {value0}점 달성", new { value0 = value });
            case UnlockConditionType.SingleHandScore: return GameText.Get("game.CollectionItemInfo.Condition.fbe6ac7975", "한 번의 제출로 {value0}점 달성", new { value0 = value });
            case UnlockConditionType.RoundClearCountInRun: return GameText.Get("game.CollectionItemInfo.Condition.c4c8ddc0de", "한 여정에서 관 {value0}회 클리어", new { value0 = value });
            case UnlockConditionType.SameJokboCountInRound: return GameText.Get("game.CollectionItemInfo.Condition.1777d6552d", "한 관에서 동일 족보 {value0}회 발동", new { value0 = value });
            case UnlockConditionType.BossClearWithoutDiscard: return GameText.Get("game.CollectionItemInfo.Condition.d5c91c9e22", "버리기 없이 보스 관 클리어");
            case UnlockConditionType.CardsConsumedInRound: return GameText.Get("game.CollectionItemInfo.Condition.75ede140c6", "한 관에서 패 {value0}장 소모", new { value0 = value });
            case UnlockConditionType.CardsDestroyedInRun: return GameText.Get("game.CollectionItemInfo.Condition.6586ce0f9e", "한 여정에서 패 {value0}장 파괴", new { value0 = value });
            case UnlockConditionType.SynergyEndingCount: return GameText.Get("game.CollectionItemInfo.Condition.b4a39580e0", "시너지 활성화 상태로 여정 {value0}회 성공", new { value0 = value });
            case UnlockConditionType.SynergyEndingCountWithTwoActive: return GameText.Get("game.CollectionItemInfo.Condition.bf19ab2512", "시너지 2종 이상 활성화 상태로 여정 {value0}회 성공", new { value0 = value });
            case UnlockConditionType.FirstRoundClearWithArtifact: return GameText.Get("game.CollectionItemInfo.Condition.ca27358164", "성주단지를 보유한 채 첫 관 클리어");
            case UnlockConditionType.Manual: return GameText.Get("game.CollectionItemInfo.Condition.c2d193484a", "특수 이벤트 달성");
            case UnlockConditionType.MoneySpentInRun: return GameText.Get("game.CollectionItemInfo.Condition.d5b7b5d807", "한 여정에서 {value0}전 이상 소모", new { value0 = value });
            case UnlockConditionType.JourneyDefeat: return GameText.Get("game.CollectionItemInfo.Condition.446d11bc59", "여정에서 패배");
            case UnlockConditionType.BadukScoreInRun: return GameText.Get("game.CollectionItemInfo.Condition.4da4a64b65", "바둑 관련 기물로 한 여정에서 {value0}점 획득", new { value0 = value });
            case UnlockConditionType.JourneyClearWithArtifact: return GameText.Get("game.CollectionItemInfo.Condition.1c6c8afc1b", "성주단지를 보유한 채 관 클리어");
            case UnlockConditionType.JourneyWinWithSameDeckCount: return GameText.Get("game.CollectionItemInfo.Condition.339a7695d4", "시작 덱 매수를 유지한 채 여정 승리");
            case UnlockConditionType.JokboLevelReached: return GameText.Get("game.CollectionItemInfo.Condition.dbbd2fbdd4", "족보 레벨 {value0} 달성", new { value0 = value });
            case UnlockConditionType.ShopRerollCountInRun: return GameText.Get("game.CollectionItemInfo.Condition.d88a57ce24", "한 여정에서 상점 다시 뽑기 {value0}회", new { value0 = value });
            case UnlockConditionType.ExorcismCountInRound: return GameText.Get("game.CollectionItemInfo.Condition.47fb199329", "한 관에서 {value0}제령 달성", new { value0 = value });
            case UnlockConditionType.UniqueArtifactsDiscovered: return GameText.Get("game.CollectionItemInfo.Condition.465268f541", "서로 다른 기물 {value0}종 발견", new { value0 = value });
            case UnlockConditionType.BossClearOnLastHand: return GameText.Get("game.CollectionItemInfo.Condition.2a2a4b8bb4", "마지막 제출 기회로 보스 클리어");
            case UnlockConditionType.BossClearWithDiscardPileCount: return GameText.Get("game.CollectionItemInfo.Condition.bf607ba6b7", "저승에 패 {value0}장 이상인 상태로 보스 클리어", new { value0 = value });
            default: return GameText.Get("game.CollectionItemInfo.Condition.8e40d737e5", "특수 조건 달성");
        }
    }
}
