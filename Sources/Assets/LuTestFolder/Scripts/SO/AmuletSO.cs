using UnityEngine;
using System.Collections.Generic;
public enum AmuletType
{
    LuckyBag, // 복주머니 [무작위 소모품 2장 생성]
    GosaDdeok, // 고사떡 [무작위 기물 1개 생성]
    Cheonmundo, // 천문도 [무작위 행성(12간지) 2장 생성]
    Meari, // 메아리 [마지막 사용 소모품 생성]
    Jebbissi, // 제비씨 [보유한 전 2배 (최대 20)]
    YeotScissors, // 엿가위 [기물 판매 가치 획득]
    Gamrosu, // 감로수 [확률로 기물, 카드에 에디션 추가]
    SamjokoFeathers, // 삼족오의 깃털 [패 1장 1/4확률 광 효과 추가]
    RedSilk, // 붉은 비단 [패 1장 홍단 강화]
    BlueSilk, // 파란 비단 [패 1장 청단 강화]
    PurpleSilk, // 보라 비단 [패 1장 초단 강화]
    BirdTalisman,       // 조 영부 [휘파람새 강화 추가칩 + 40]
    BloodTalisman,      // 혈 영부 [두견새 강화 25퍼 확률 배수 2배]
    ButterflyTalisman,  // 호 영부 [나비 강화 50퍼 확률 추가칩 + 60]
    BoarTalisman,       // 저 영부 [멧돼지 강화 배수 +5, 덱 3장 버림]
    GooseTalisman,      // 안 영부 [기러기 강화 손패 보유 시 배수 1.5배]
    DeerTalisman,       // 록 영부 [사슴 강화 보유 시 라운드 종료 3전 획득]
    ManseCalendar,      // 만세력 [패 1장 n월로 변경]
    TransformationLeaf, // 둔갑 잎사귀 [첫 번째 선택 카드를 두 번째 선택 카드로 변경]
    Tteokguk,           // 떡국 [패 1장 랭크(숫자) 1 증가]
    MagicJar,           // 요술 항아리 [선택 카드 복사]
    RedBeanPorridge,    // 팥죽 [카드 n장 제거(파괴)]
    PlanetReplay       // 메아리로 생성된 신장패
}

public enum EnhancementCategory { None, Dan, YulGgeut, Gwang, Edition }

public static class ConsumableUseRules
{
    public static bool IsBattleHandTargetConsumable(AmuletSO amulet)
    {
        return amulet != null && IsBattleHandTargetConsumable(amulet.amuletType);
    }

    public static bool IsBattleHandTargetConsumable(AmuletType type)
    {
        return type == AmuletType.MagicJar ||
               type == AmuletType.ManseCalendar ||
               type == AmuletType.TransformationLeaf ||
               type == AmuletType.Tteokguk;
    }

    public static bool IsBattleHandCopyConsumable(AmuletSO amulet)
    {
        return amulet != null && IsBattleHandCopyConsumable(amulet.amuletType);
    }

    public static bool IsBattleHandCopyConsumable(AmuletType type)
    {
        return type == AmuletType.MagicJar ||
               type == AmuletType.TransformationLeaf;
    }

    public static bool CanUseFromPreview(AmuletSO amulet)
    {
        if (amulet == null)
            return false;

        if (amulet.amuletType == AmuletType.GosaDdeok)
            return ArtifactManager.Instance != null
                && ArtifactManager.Instance.HasSpaceForAnyArtifact();

        if (IsBattleHandTargetConsumable(amulet))
            return IsBattleActive() && HasBattleHandCards(amulet.minTargetCount);

        return true;
    }

    public static bool RequiresInventorySlotReleasedBeforeEffect(AmuletSO amulet)
    {
        return amulet != null && (amulet.amuletType == AmuletType.LuckyBag || amulet.amuletType == AmuletType.Meari);
    }

    public static bool IsBattleActive()
    {
        return CombatManager.instance != null && CombatManager.instance.isInCombat;
    }

    public static bool HasBattleHandCards(int requiredCount = 1)
    {
        if (!IsBattleActive())
            return false;

        int safeRequiredCount = Mathf.Max(1, requiredCount);
        return CardManager_2.Instance != null &&
               CardManager_2.Instance.handCardBuffer != null &&
               CardManager_2.Instance.handCardBuffer.Count >= safeRequiredCount;
    }
}

[CreateAssetMenu (fileName = "New Amulet", menuName = "Card/AmuletSO")]
public class AmuletSO : BaseCardSO
{
    public override CardType cardType => CardType.Amulet;

    [Header("부적 기본 설정")]
    public AmuletType amuletType;
    [Min(0)] public int amuletPrice = 4;

    [Header("테스트 부적 모델링 및 혹시 스프라이트")]
    public GameObject amuletModelPrefab;
    public Sprite amuletSprite;

    [Header("부적 설명")]
    [Tooltip("인게임에 표시할 효과 설명입니다.")]
    [TextArea(3,5)]
    public string amuletDescription;
    [TextArea(3, 5)] public string amuletLoreDescription;

    [Header("사용 조건")]
    [Tooltip("효과를 발동하기 위해 선택해야 할 핸드 카드의 최대 개수 (0이면 대상 불필요)")]
    public int maxTargetCount = 0;
    public int minTargetCount = 0;
    public PlanetSO replayPlanet;

    [Header("필터링용: 이 부적은 무엇을 강화하나요?")]
    // 이 변수를 추가해 줍니다!
    public EnhancementCategory targetCategory = EnhancementCategory.None;



    public bool TryUseAmulet(List<CardSO> selectedCards = null)
    {
        int currentSelectedCount = selectedCards != null ? selectedCards.Count : 0;

        // 1. 선택한 카드 개수가 요구 조건에 맞는지 검사
        if (currentSelectedCount < minTargetCount || currentSelectedCount > maxTargetCount)
        {
            Debug.LogWarning($"[부적 사용 실패] {cardName}을(를) 사용하려면 카드를 {minTargetCount}장 ~ {maxTargetCount}장 선택해야 합니다.");
            return false;
        }

        // 2. 부적 효과 발동 (아래에서 switch문으로 각각의 효과를 처리합니다)
        bool isSuccess = ActivateAmuletEffect(selectedCards);

        if (isSuccess)
        {
            Debug.Log($"<color=cyan>[부적 발동]</color> {cardName} 사용 성공!");
            ArtifactManager.Instance?.ProcessArtifactTrigger(ArtifactTriggerType.OnAmuletUse);
        }

        return isSuccess;
    }

    private bool ActivateAmuletEffect(List<CardSO> selectedCards)
    {
        // 복주머니 천문도
        switch (amuletType)
        {
            case AmuletType.PlanetReplay:
                if (replayPlanet == null || PlanetCardLevelManager.Instance == null) return false;
                PlanetCardLevelManager.Instance.UpgradeJokboLevel(replayPlanet);
                return true;
            case AmuletType.LuckyBag: // 복주머니 [무작위 소모품 2장 생성]
            Debug.Log("복주머니 사용 완료");
                if (ConsumableManager.Instance != null)
                {
                    // 매니저에게 랜덤 부적 2장 생성을 요청합니다.
                    ConsumableManager.Instance.AddRandomConsumable(2, ResolveSourceName());
                    return true;
                }
                return false;

            case AmuletType.GosaDdeok: // 고사떡 [무작위 기물 1개 생성]
            Debug.Log("고사떡 사용 완료");
                if (ArtifactManager.Instance != null)
                {
                    ArtifactData randomArtifact = PickRandomAvailableArtifact();
                    if (randomArtifact == null)
                    {
                        Debug.LogWarning("기물 칸이 가득 찼거나 생성할 수 있는 기물이 없습니다!");
                        return false;
                    }

                    bool added = ArtifactManager.Instance.TryAddArtifact(randomArtifact);
                    if (!added)
                    {
                        Debug.LogWarning("기물 생성에 실패했습니다.");
                        return false;
                    }

                    Debug.Log($"<color=cyan>[고사떡 발동]</color> 무작위 기물 '{randomArtifact.artifactName}' 생성!");
                    return true;
                }
                return false;

            case AmuletType.Cheonmundo: // 천문도 [무작위 행성(12간지) 2장 생성]
            Debug.Log("천문도 사용 완료");
                if (PlanetCardLevelManager.Instance != null)
                {
                    // 행성은 슬롯이 없으므로, 생성되자마자 즉시 '중복 없이 2장'을 뽑아 강화합니다.
                    PlanetCardLevelManager.Instance.RandomUpgradeDistinct(2, true);
                    
                    Debug.Log("<color=cyan>[천문도 발동]</color> 서로 다른 행성 2개가 관측되어 족보가 강화되었습니다!");
                    return true;
                }
                else
                {
                    Debug.LogWarning("PlanetCardLevelManager가 없어서 천문도를 쓸 수 없습니다.");
                    return false;
                }

            case AmuletType.Meari: // 메아리 [마지막 사용 소모품 생성] // 생성만 해야됌
                if (ConsumableManager.Instance != null)
                {
                    AmuletSO toRestore = ConsumableManager.Instance.lastUsedAmulet;
                    if (toRestore != null)
                    {
                        bool success = ConsumableManager.Instance.TryAddConsumable(toRestore, ResolveSourceName());
                        if (success) {
                            Debug.Log($"<color=green>[메아리 발동]</color> '{toRestore.cardName}' 부적이 복구되었습니다!");
                            return true;
                        } else {
                            Debug.LogWarning("인벤토리가 가득 차서 메아리 효과를 적용할 수 없습니다.");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("이번 게임에서 아직 사용한 부적이 없어서 메아리가 칠 수 없습니다!");
                        return false;
                    }
                }
                return false;

            case AmuletType.Jebbissi: // 제비씨 [보유한 전 2배 (최대 20)]
                if (EconomyManager.instance != null)
                {
                    int currentMoney = EconomyManager.instance.ecoData.currentMoney;
                    int addAmount = Mathf.Min(currentMoney, 20); // 현재 돈만큼 주되, 상한선 20 제한
                    
                    if (addAmount <= 0) 
                    {
                        Debug.LogWarning("돈이 없어서 제비씨 효과를 볼 수 없습니다!");
                        return false; // 돈이 없으면 사용 불가능 처리 (소모 안됨)
                    }

                    EconomyManager.instance.AddMoney(addAmount);
                    return true;
                }
                return false;

            case AmuletType.YeotScissors: // 엿가위 [기물 판매 가치 획득]
                if (ArtifactManager.Instance != null && EconomyManager.instance != null)
                {
                    int totalSellValue = 0;
                    // 인벤토리에 있는 모든 기물의 '현재 판매가'를 긁어모읍니다.
                    for (int i = 0; i < ArtifactManager.Instance.inventory.items.Count; i++)
                    {
                        totalSellValue += ArtifactManager.Instance.GetArtifactSellPrice(i);
                    }

                    totalSellValue = Mathf.Min(50, totalSellValue);

                    if (totalSellValue > 0)
                    {
                        EconomyManager.instance.AddMoney(totalSellValue);
                        Debug.Log($"<color=cyan>[엿가위 발동]</color> 기물 가치 {totalSellValue}전을 획득했습니다!");
                        return true; // 성공
                    }
                    else
                    {
                        Debug.LogWarning("가진 기물이 없거나 가치가 0전입니다.");
                        return false; // 돈이 0원이면 부적 소모 안 함
                    }
                }
                return false;

            case AmuletType.Gamrosu: // 감로수 [확률로 기물, 카드에 에디션 추가]
            // 에디션이 없는 애들만 가져오기 [기물 , 카드]
                List<CardSO> validCards = new List<CardSO>();
                if (GameManager.instance != null)
                {
                    foreach (var card in GameManager.instance.masterDeck)
                    {
                        if (card is CardSO cardSO && cardSO.currentEdition == EditionType.None)
                        {
                            validCards.Add(cardSO);
                        }
                    }
                }

                List<int> validArtifactIndices = new List<int>();
                if (ArtifactManager.Instance != null && ArtifactManager.Instance.inventory != null)
                {
                    for (int i = 0; i < ArtifactManager.Instance.inventory.items.Count; i++)
                    {
                        if (ArtifactManager.Instance.GetArtifactEdition(i) == EditionType.None)
                            validArtifactIndices.Add(i);
                    }
                }
                // 타겟 합치기
                int totalValidTargets = validCards.Count + validArtifactIndices.Count;

                // 3. 방어 코드: 바를 대상이 하나도 없으면 사용 취소
                if (totalValidTargets == 0)
                {
                    Debug.LogWarning("에디션을 부여할 깨끗한 카드나 기물이 하나도 없습니다!");
                    return false; 
                }

                // 4. 1/4 확률 검사 (실패해도 부적은 소모됨)
                if (ProbabilityResolver.RollChance(1f / 3f))
                {
                    // 1:은박, 2:금박, 3:찬란 중 랜덤
                    EditionType randomEdition = (EditionType)UnityEngine.Random.Range(1, 4); 
                    
                    // 통합된 전체 개수 안에서 무작위 번호 하나를 뽑습니다.
                    int randomIndex = UnityEngine.Random.Range(0, totalValidTargets);

                    // 뽑힌 번호가 카드 리스트 안에 속한다면 -> 카드 당첨!
                    if (randomIndex < validCards.Count)
                    {
                        CardSO targetCard = validCards[randomIndex];
                        targetCard.currentEdition = randomEdition;
                        Debug.Log($"<color=cyan>[감로수 성공]</color> 덱에 있는 카드 '{targetCard.cardName}'에 {randomEdition} 에디션 추가!");

                        // 디테일: 만약 당첨된 카드가 지금 내 손패(화면)에 들려있다면, 이미지를 즉시 갱신해줍니다.
                        if (CardManager_2.Instance != null)
                        {
                            var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                            if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                        }
                    }
                    // 뽑힌 번호가 카드 개수보다 크다면 -> 기물 당첨!
                    else
                    {
                        // 전체 인덱스에서 카드 개수만큼 빼면 기물 리스트의 인덱스가 됩니다.
                        int targetArtifactIndex = validArtifactIndices[randomIndex - validCards.Count];
                        ArtifactData targetArtifact = ArtifactManager.Instance.inventory.items[targetArtifactIndex];
                        ArtifactManager.Instance.SetArtifactEdition(targetArtifactIndex, randomEdition);
                        Debug.Log($"<color=cyan>[감로수 성공]</color> 장착 중인 기물 '{targetArtifact.artifactName}'에 {randomEdition} 에디션 추가!");
                    }
                }
                else
                {
                    Debug.Log($"<color=gray>[감로수 실패]</color> 에디션 부여에 실패했습니다. ({ProbabilityResolver.FormatChance(1f / 3f)} 빗나감)");
                }

                return true; // 대상이 존재해서 사용을 시도했으므로 부적 소모

            case AmuletType.SamjokoFeathers: // 삼족오의 깃털 [패 1장 1/4확률 광 효과 추가]
                if(selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // CardController 제거, CardSO 직접 사용
                    
                    if(targetCard.currentGwang == GwangEnhancement.Gwang) return false;

                    if(ProbabilityResolver.RollChance(0.25f))
                    {
                        targetCard.currentGwang = GwangEnhancement.Gwang;
                        
                        // 손패 동기화
                        if(CardManager_2.Instance != null)
                        {
                            var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                            if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                        }
                        Debug.Log($"<color=blue>[강화 성공]</color> 삼족오의 깃털 ({ProbabilityResolver.FormatChance(0.25f)} 성공)");
                    }
                    else
                    {
                        Debug.Log($"<color=blue>[강화 실패]</color> 삼족오의 깃털 ({ProbabilityResolver.FormatChance(0.25f)} 빗나감)");
                    }
                    return true;
                }
                return false;

            case AmuletType.RedSilk: // 붉은 비단 [패 1장 홍단 강화]
            Debug.Log("붉은 비단 사용 완료");
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentDan == DanEnhancement.HongDan) return false; 

                    targetCard.currentDan = DanEnhancement.HongDan;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;
            
            case AmuletType.BlueSilk: // 파란 비단 [패 1장 청단 강화]
            Debug.Log("파란 비단 사용 완료");
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentDan == DanEnhancement.CheongDan) return false; 

                    targetCard.currentDan = DanEnhancement.CheongDan;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.PurpleSilk: // 보라 비단 [패 1장 초단 강화]
            Debug.Log("보라 비단 사용 완료");
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentDan == DanEnhancement.ChoDan) return false; 

                    targetCard.currentDan = DanEnhancement.ChoDan;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.BirdTalisman: // 조 영부 [휘파람새 강화 추가칩 + 40]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Warbler) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Warbler;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.BloodTalisman: // 혈 영부 [두견새 강화 25퍼 확률 배수 2배]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Cuckoo) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Cuckoo;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.ButterflyTalisman: // 호 영부 [나비 강화 50퍼 확률 추가칩 + 60]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Butterfly) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Butterfly;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.BoarTalisman: // 저 영부 [멧돼지 강화 배수 +5, 덱 3장 버림]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Boar) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Boar;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.GooseTalisman: // // 안 영부 [기러기 강화 손패 보유 시 배수 1.5배]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Goose) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Goose;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.DeerTalisman: // 록 영부 [사슴 강화 보유 시 라운드 종료 3전 획득]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // 바로 CardSO를 꺼내 씁니다.
                    if (targetCard.currentYulGgeut == YulGgeutEnhancement.Deer) return false; 

                    targetCard.currentYulGgeut = YulGgeutEnhancement.Deer;
                    
                    // ※ 주의: 덱 안의 데이터를 수정했으므로, 
                    // 혹시 이 카드가 우연히 내 '손패'에도 있다면 이미지를 업데이트 해줘야 합니다.
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    return true;
                }
                return false;

            case AmuletType.ManseCalendar: // 만세력 [패 1장 n월로 변경]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // CardSO 직접 사용

                    // 1월(인덱스 0) ~ 10월(인덱스 9) 사이로 무작위 변경
                    targetCard.cardMonth = (_CardMonth)UnityEngine.Random.Range(0, 10);
                    CardUpgradeService.NormalizeScoreForCurrentState(targetCard);
                    
                    // 손패 동기화
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }

                    Debug.Log($"<color=cyan>[만세력 발동]</color> 카드가 {targetCard.cardMonth}월로 변경되었습니다!");
                    return true;
                }
                return false;

            case AmuletType.TransformationLeaf: // 둔갑 잎사귀 [첫 번째 선택 카드를 두 번째 선택 카드로 복사]
                if (selectedCards != null && selectedCards.Count == 2)
                {
                    // 선택 UI는 첫 번째 선택을 변할 대상으로, 두 번째 선택을
                    // 복사 원본으로 안내하므로 효과 적용 순서도 같은 기준을 사용합니다.
                    CardSO targetCard = selectedCards[0]; // 변할 대상
                    CardSO sourceCard = selectedCards[1]; // 복사 원본

                    targetCard.cardID = sourceCard.cardID;
                    targetCard.cardName = sourceCard.cardName;
                    targetCard.cardMonth = sourceCard.cardMonth;
                    targetCard.ggeutValue = sourceCard.ggeutValue;
                    targetCard.isGwang = sourceCard.isGwang;
                    targetCard.cardDescription = sourceCard.cardDescription;
                    targetCard.currentDan = sourceCard.currentDan;
                    targetCard.currentYulGgeut = sourceCard.currentYulGgeut;
                    targetCard.currentGwang = sourceCard.currentGwang;
                    targetCard.currentEdition = sourceCard.currentEdition;
                    targetCard.cardUpgradeLevel = sourceCard.cardUpgradeLevel;
                    targetCard.isDestroyed = sourceCard.isDestroyed;
                    targetCard.monthSprite = sourceCard.monthSprite;
                    targetCard.backSprite = sourceCard.backSprite;
                    targetCard.normalSprite = sourceCard.normalSprite;
                    targetCard.gwangSprite = sourceCard.gwangSprite;
                    CardUpgradeService.NormalizeScoreForCurrentState(targetCard);
                    
                    // 손패 동기화 (변한 카드가 손패에 들려있을 경우)
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    
                    Debug.Log($"<color=cyan>[둔갑 잎사귀 발동]</color> 카드 복사 완료!");
                    return true;
                }
                return false;

            case AmuletType.Tteokguk: // 떡국 [패 1장 랭크(숫자) 1 증가]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    CardSO targetCard = selectedCards[0]; // CardSO 직접 사용
                    int currentMonthIndex = (int)targetCard.cardMonth;
                    
                    if (currentMonthIndex < 9) 
                    {
                        targetCard.cardMonth = (_CardMonth)(currentMonthIndex + 1);
                        Debug.Log($"<color=cyan>[떡국 발동]</color> 카드 랭크가 1 올랐습니다!");
                    }
                    else if (currentMonthIndex == 9)
                    {
                        targetCard.cardMonth = (_CardMonth)(currentMonthIndex - 9); // 10월이면 1월로
                        Debug.Log($"<color=cyan>[떡국 발동]</color> 카드 랭크가 1월로 돌아갔습니다!");
                    }
                    CardUpgradeService.NormalizeScoreForCurrentState(targetCard);

                    // 손패 동기화
                    if(CardManager_2.Instance != null)
                    {
                        var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                        if (handCard != null) handCard.GetComponent<CardSetting>().Setup(targetCard);
                    }
                    return true;
                }
                return false;

            case AmuletType.MagicJar: // 요술 항아리 [선택 카드 복사하여 덱에 추가]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    if (!ConsumableUseRules.IsBattleActive())
                    {
                        Debug.LogWarning("요술 항아리는 전투 중 손패가 있을 때만 사용할 수 있습니다.");
                        return false;
                    }

                    if (CardManager_2.Instance == null)
                    {
                        Debug.LogWarning("카드 매니저가 없어 요술 항아리를 사용할 수 없습니다.");
                        return false;
                    }

                    CardSO sourceCard = selectedCards[0];
                    CardSO clonedCard = Instantiate(sourceCard);
                    clonedCard.name = sourceCard.name + "_Clone";

                    // 1. 공통: 전체 가방(Master Deck)에 영구 추가
                    if (GameManager.instance != null)
                    {
                        GameManager.instance.masterDeck.Add(clonedCard);
                    }
                    CardManager_2.Instance.ForceAddCardToHand(clonedCard);
                    Debug.Log($"<color=cyan>[요술 항아리 발동]</color> '{sourceCard.cardName}' 복사 완료! (전투 중: 손패 추가)");
                    return true;
                }
                return false;

            case AmuletType.RedBeanPorridge: // 팥죽 [선택한 카드 영구 파괴]
                if (selectedCards != null && selectedCards.Count == 1)
                {
                    foreach (var targetCard in selectedCards)
                    {
                        // 1. 가방(마스터 덱)에서 영구 삭제
                        if (GameManager.instance != null && GameManager.instance.masterDeck.Remove(targetCard))
                            UnlockManager.instance?.RecordCardsDestroyed(1);
                        
                        // 2. 현재 전투 중인 구역들에서도 삭제
                        if (CardManager_2.Instance != null)
                        {
                            CardManager_2.Instance.cardBuffer.Remove(targetCard);
                            CardManager_2.Instance.handCardBuffer.Remove(targetCard);
                            CardManager_2.Instance.dropCardBuffer.Remove(targetCard);
                            CardManager_2.Instance.RefreshRemainingDeckCountUI();
                            
                            // 만약 화면(손패)에 들고 있던 카드라면 파괴 연출
                            var handCard = CardManager_2.Instance.myHandCards.Find(c => c.cardData == targetCard);
                            if (handCard != null)
                            {
                                CardManager_2.Instance.myHandCards.Remove(handCard);
                                Destroy(handCard.gameObject);
                            }
                        }
                    }
                    
                    if(CardManager_2.Instance != null) CardManager_2.Instance.AlignCards(false, null);
                    
                    Debug.Log($"<color=cyan>[팥죽 발동]</color> 선택한 카드가 영구적으로 파괴되었습니다!");
                    return true;
                }
                return false;

            // TODO: 나머지 부적 효과들도 이곳에 추가해 나가면 됩니다!
            default:
                Debug.LogWarning("아직 구현되지 않은 부적입니다: " + amuletType);
                return false;
        }
    }

    private string ResolveSourceName()
    {
        if (!string.IsNullOrWhiteSpace(cardName))
            return cardName;

        return string.IsNullOrWhiteSpace(name) ? GameText.Get("game.AmuletSO.ResolveSourceName.905e5ba805", "소모품") : name;
    }

    private ArtifactData PickRandomAvailableArtifact()
    {
        List<ArtifactData> pool = new List<ArtifactData>();

        if (UnlockManager.instance != null && UnlockManager.instance.unlockedArtifacts != null)
        {
            for (int i = 0; i < UnlockManager.instance.unlockedArtifacts.Count; i++)
            {
                ArtifactData artifact = UnlockManager.instance.unlockedArtifacts[i];
                if (IsArtifactSpawnCandidate(artifact))
                    pool.Add(artifact);
            }
        }

        if (pool.Count == 0 && UnlockManager.instance != null && UnlockManager.instance.itemDatabase != null)
        {
            List<ArtifactData> allArtifacts = UnlockManager.instance.itemDatabase.allArtifacts;
            if (allArtifacts != null)
            {
                for (int i = 0; i < allArtifacts.Count; i++)
                {
                    ArtifactData artifact = allArtifacts[i];
                    if (IsArtifactSpawnCandidate(artifact))
                        pool.Add(artifact);
                }
            }
        }

        if (pool.Count == 0)
            return null;

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private static bool IsArtifactSpawnCandidate(ArtifactData artifact)
    {
        if (artifact == null)
            return false;

        if (ArtifactManager.Instance == null || ArtifactManager.Instance.inventory == null)
            return true;

        if (!ArtifactManager.Instance.CanAddArtifact(artifact))
            return false;

        string artifactId = artifact.artifactID;
        if (string.IsNullOrWhiteSpace(artifactId))
            return true;

        foreach (ArtifactData ownedArtifact in ArtifactManager.Instance.inventory.items)
        {
            if (ownedArtifact != null && ownedArtifact.artifactID == artifactId)
                return false;
        }

        return true;
    }
}
