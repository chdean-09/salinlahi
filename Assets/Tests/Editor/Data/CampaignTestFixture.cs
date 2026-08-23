using System;
using System.Collections.Generic;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    public sealed class CampaignTestFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        public CampaignConfigSO Campaign { get; private set; }

        private CampaignTestFixture()
        {
        }

        public static CampaignTestFixture CreateValid()
        {
            CampaignTestFixture fixture = new();
            fixture.BuildValidCampaign();
            return fixture;
        }

        public void Dispose()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object createdObject = _createdObjects[i];
                if (createdObject != null)
                    UnityEngine.Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
            Campaign = null;
        }

        private void BuildValidCampaign()
        {
            Campaign = Track(ScriptableObject.CreateInstance<CampaignConfigSO>());
            Campaign.manifest = CampaignIdentityManifest.CreateRevisedV1();
            Campaign.tuning = new CampaignTuning { defaultShrineHearts = 3 };
            Campaign.learningTuning = Track(ScriptableObject.CreateInstance<LearningTuningSO>());

            Sprite contextSprite = CreateContextSprite();
            AudioClip narrationClip = Track(AudioClip.Create("SALIN170_Test", 64, 1, 44100, false));
            DialogueSO dialogue = Track(ScriptableObject.CreateInstance<DialogueSO>());
            dialogue.lines = new[]
            {
                new DialogueLine { speakerName = "Test", text = "Synthetic content." },
            };
            CutsceneSO cutscene = Track(ScriptableObject.CreateInstance<CutsceneSO>());
            cutscene.panels = new[]
            {
                new CutscenePanel { image = contextSprite, text = "Synthetic memory." },
            };

            Dictionary<string, BaybayinCharacterSO> symbolsById = CreateSymbols();
            CreateErasAndLevels(symbolsById, contextSprite, narrationClip, dialogue, cutscene);
        }

        private Dictionary<string, BaybayinCharacterSO> CreateSymbols()
        {
            var symbolsById = new Dictionary<string, BaybayinCharacterSO>(StringComparer.Ordinal);
            for (int i = 0; i < ContentIdentity.RevisedSymbolIds.Count; i++)
            {
                string stableId = ContentIdentity.RevisedSymbolIds[i];
                BaybayinCharacterSO symbol = Track(ScriptableObject.CreateInstance<BaybayinCharacterSO>());
                symbol.stableId = stableId;
                symbol.characterID = stableId.Substring("symbol.".Length).ToUpperInvariant();
                symbol.syllable = stableId.Substring("symbol.".Length);
                symbol.firstIntroductionLevelId = ContentIdentity.RevisedLevelIds[Math.Min(i, 14)];
                symbol.legacyAliases = new List<string>();

                string primaryValueId = GetPrimaryValueId(stableId);
                symbol.spokenValues = new List<SpokenValueDefinition>
                {
                    new SpokenValueDefinition
                    {
                        stableId = primaryValueId,
                        displayValue = stableId == "symbol.dara" ? "DA" : symbol.syllable.ToUpperInvariant(),
                    },
                };

                if (stableId == "symbol.dara")
                {
                    symbol.legacyAliases.Add("DA");
                    symbol.legacyAliases.Add("RA");
                    symbol.spokenValues.Add(new SpokenValueDefinition
                    {
                        stableId = "value.ra",
                        displayValue = "RA",
                    });
                }

                Campaign.symbols.Add(symbol);
                symbolsById.Add(stableId, symbol);
            }

            return symbolsById;
        }

        private void CreateErasAndLevels(
            Dictionary<string, BaybayinCharacterSO> symbolsById,
            Sprite contextSprite,
            AudioClip narrationClip,
            DialogueSO dialogue,
            CutsceneSO cutscene)
        {
            int globalOrder = 1;
            for (int eraIndex = 0; eraIndex < ContentIdentity.RevisedEraIds.Count; eraIndex++)
            {
                string eraId = ContentIdentity.RevisedEraIds[eraIndex];
                EraConfigSO era = Track(ScriptableObject.CreateInstance<EraConfigSO>());
                era.stableId = eraId;
                era.order = eraIndex + 1;
                era.eraName = eraId.Substring("era.".Length).ToUpperInvariant();
                era.storyReference = dialogue;
                era.memoryReference = cutscene;
                Campaign.eras.Add(era);

                for (int localOrder = 1; localOrder <= 5; localOrder++)
                {
                    LevelConfigSO level = Track(ScriptableObject.CreateInstance<LevelConfigSO>());
                    level.levelName = "Synthetic " + globalOrder;
                    level.levelNumber = globalOrder;
                    level.stableId = ContentIdentity.GetLevelId(eraId, localOrder);
                    level.eraLocalOrder = localOrder;
                    level.defenseRules = new DefenseRules();
                    level.contextMedia = CreateMediaReferences(contextSprite, narrationClip, dialogue, cutscene);
                    level.rewardIds = new List<string> { "reward." + globalOrder.ToString("00") };

                    List<string> introducedSymbolIds = GetIntroducedSymbolIds(globalOrder);
                    BaybayinCharacterSO focusSymbol = level.stableId == "level.pamana.05"
                        ? symbolsById["symbol.nga"]
                        : symbolsById[introducedSymbolIds[introducedSymbolIds.Count - 1]];
                    string focusValueId = GetPrimaryValueId(focusSymbol.stableId);
                    level.focusWords = new List<FocusWordDefinition>
                    {
                        CreateFocusWord(level.stableId + ".focus.01", focusSymbol, focusValueId,
                            contextSprite, narrationClip, dialogue, cutscene),
                        CreateFocusWord(level.stableId + ".focus.02", focusSymbol, focusValueId,
                            contextSprite, narrationClip, dialogue, cutscene),
                    };

                    level.cumulativeSymbolPool = new List<SymbolValueReference>();
                    for (int i = 0; i < introducedSymbolIds.Count; i++)
                    {
                        string symbolId = introducedSymbolIds[i];
                        level.cumulativeSymbolPool.Add(new SymbolValueReference
                        {
                            symbol = symbolsById[symbolId],
                            spokenValueId = GetPrimaryValueId(symbolId),
                        });
                    }

                    level.learningRequirements = new List<ContentRequirement>();
                    level.practiceRequirements = new List<ContentRequirement>();
                    level.masteryRequirements = new List<ContentRequirement>();
                    AddRequirement(level.learningRequirements, ContentRequirementKind.Instruction,
                        focusSymbol, focusValueId);
                    AddRequirement(level.practiceRequirements, ContentRequirementKind.Practice,
                        focusSymbol, focusValueId);
                    AddRequirement(level.masteryRequirements, ContentRequirementKind.Mastery,
                        focusSymbol, focusValueId);
                    level.finalRestorationValue = new SymbolValueReference
                    {
                        symbol = focusSymbol,
                        spokenValueId = focusValueId,
                    };

                    if (level.stableId == "level.pamana.05")
                    {
                        AddRequirement(level.learningRequirements, ContentRequirementKind.Instruction,
                            symbolsById["symbol.pa"], "value.pa");
                        AddRequirement(level.practiceRequirements, ContentRequirementKind.Assessment,
                            symbolsById["symbol.pa"], "value.pa");
                        level.focusWords[1].decomposition.Add(new SymbolValueReference
                        {
                            symbol = symbolsById["symbol.pa"],
                            spokenValueId = "value.pa",
                        });
                        level.finalRestorationValue = new SymbolValueReference
                        {
                            symbol = symbolsById["symbol.pa"],
                            spokenValueId = "value.pa",
                        };
                    }

                    era.levels.Add(level);
                    globalOrder++;
                }
            }
        }

        private static FocusWordDefinition CreateFocusWord(
            string stableId,
            BaybayinCharacterSO symbol,
            string spokenValueId,
            Sprite contextSprite,
            AudioClip narrationClip,
            DialogueSO dialogue,
            CutsceneSO cutscene)
        {
            var focusWord = new FocusWordDefinition
            {
                stableId = stableId,
                latinSpelling = symbol.syllable,
                displayLabel = symbol.syllable.ToUpperInvariant(),
                meaning = "Synthetic meaning for " + symbol.syllable + ".",
                media = CreateMediaReferences(contextSprite, narrationClip, dialogue, cutscene),
            };
            focusWord.decomposition.Add(new SymbolValueReference
            {
                symbol = symbol,
                spokenValueId = spokenValueId,
            });
            return focusWord;
        }

        private static ContentMediaReferences CreateMediaReferences(
            Sprite contextSprite,
            AudioClip narrationClip,
            DialogueSO dialogue,
            CutsceneSO cutscene)
        {
            return new ContentMediaReferences
            {
                contextImage = contextSprite,
                narrationClip = narrationClip,
                dialogue = dialogue,
                cutscene = cutscene,
            };
        }

        private static void AddRequirement(
            List<ContentRequirement> requirements,
            ContentRequirementKind kind,
            BaybayinCharacterSO symbol,
            string spokenValueId)
        {
            requirements.Add(new ContentRequirement
            {
                kind = kind,
                requiredSuccesses = 1,
                symbolValue = new SymbolValueReference
                {
                    symbol = symbol,
                    spokenValueId = spokenValueId,
                },
            });
        }

        private static List<string> GetIntroducedSymbolIds(int globalOrder)
        {
            var ids = new List<string>();
            for (int i = 0; i < ContentIdentity.RevisedSymbolIds.Count; i++)
            {
                if (i + 1 <= globalOrder || i >= 15 && globalOrder == 15)
                    ids.Add(ContentIdentity.RevisedSymbolIds[i]);
            }

            return ids;
        }

        private static string GetPrimaryValueId(string symbolId)
        {
            return symbolId == "symbol.dara"
                ? "value.da"
                : "value." + symbolId.Substring("symbol.".Length);
        }

        public ChallengeSequenceSO CreateValidChallengeSequence()
        {
            ChallengeSequenceSO sequence = Track(ScriptableObject.CreateInstance<ChallengeSequenceSO>());
            sequence.sequenceId = "challenge.synthetic.01";
            sequence.displayName = "Synthetic Challenge";
            sequence.units = new[]
            {
                new ChallengeUnitDefinition
                {
                    unitId = "unit.synthetic.01",
                    mode = ChallengeMode.WordPlacement,
                    cluePolicy = ChallengeCluePolicy.Full,
                    prompt = "Restore the synthetic word.",
                    tokens = new[]
                    {
                        new ChallengeTokenDefinition
                        {
                            tokenId = "token.synthetic.01",
                            displayText = "A",
                            occurrenceId = "occurrence.synthetic.01",
                            role = ChallengeTokenRole.Focus,
                        },
                    },
                    slots = new[]
                    {
                        new ChallengeSlotDefinition
                        {
                            slotId = "slot.synthetic.01",
                            expectedOccurrenceId = "occurrence.synthetic.01",
                        },
                    },
                    candidateOccurrenceIds = new[] { "occurrence.synthetic.01" },
                    allowHint = true,
                    checkpointOnSuccess = true,
                    maxErrors = 3,
                    heartPenalty = 1,
                },
            };
            return sequence;
        }

        private Sprite CreateContextSprite()
        {
            Texture2D texture = Track(new Texture2D(2, 2));
            return Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f));
        }

        private T Track<T>(T createdObject) where T : UnityEngine.Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
