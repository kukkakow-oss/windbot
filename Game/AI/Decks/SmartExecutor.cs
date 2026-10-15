using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Data.Sqlite;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    // Bot Theater: a general-purpose AI for random decks.
    //
    // WindBot's good AIs are written by hand for one deck each, and the "Lucky" AI
    // simply does everything it is allowed to, in random order. This one reads each
    // card's text to get a rough idea of what it does (draws, searches, Special
    // Summons, removes cards, negates, wipes the whole field, costs LP...) and uses
    // that to decide when a card is worth playing and what to aim it at:
    //  - card draw and searches first, then summons, then everything else
    //  - field wipes only when the opponent would lose more than we would
    //  - removal that picks out cards one at a time only when the opponent has a card
    //    it can hit (otherwise it would have to hit one of ours)
    //  - set Spells/Traps are kept for the opponent's turn (their summons, attacks,
    //    or End Phase) unless they help us right now
    //  - negation only against the opponent, never against our own plays
    //  - removal is aimed at the opponent's most threatening card; costs, discards
    //    and materials use our least valuable cards
    //  - monsters that can't win a fight go in Defense Position or face-down
    //  - no effects that cost more LP than we have
    [Deck("Smart", "AI_Test", "Test")]
    public class SmartExecutor : DefaultExecutor
    {
        public SmartExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Card-specific logic WindBot already has for common staples.
            AddExecutor(ExecutorType.Activate, _CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, _CardId.CosmicCyclone, DefaultCosmicCyclone);
            AddExecutor(ExecutorType.Activate, _CardId.GalaxyCyclone, DefaultGalaxyCyclone);
            AddExecutor(ExecutorType.Activate, _CardId.BookOfMoon, DefaultBookOfMoon);
            AddExecutor(ExecutorType.Activate, _CardId.CompulsoryEvacuationDevice, DefaultCompulsoryEvacuationDevice);
            AddExecutor(ExecutorType.Activate, _CardId.CallOfTheHaunted, DefaultCallOfTheHaunted);
            AddExecutor(ExecutorType.Activate, _CardId.Scapegoat, DefaultScapegoat);
            AddExecutor(ExecutorType.Activate, _CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, _CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, _CardId.GhostOgreAndSnowRabbit, DefaultGhostOgreAndSnowRabbit);
            AddExecutor(ExecutorType.Activate, _CardId.GhostBelle, DefaultGhostBelleAndHauntedMansion);
            AddExecutor(ExecutorType.Activate, _CardId.EffectVeiler, DefaultEffectVeiler);
            AddExecutor(ExecutorType.Activate, _CardId.CalledByTheGrave, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, _CardId.InfiniteImpermanence, DefaultInfiniteImpermanence);
            AddExecutor(ExecutorType.Activate, _CardId.BreakthroughSkill, DefaultBreakthroughSkill);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnJudgment, DefaultSolemnJudgment);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnWarning, DefaultSolemnWarning);
            AddExecutor(ExecutorType.Activate, _CardId.SolemnStrike, DefaultSolemnStrike);
            AddExecutor(ExecutorType.Activate, _CardId.TorrentialTribute, DefaultTorrentialTribute);
            AddExecutor(ExecutorType.Activate, _CardId.HeavyStorm, DefaultHeavyStorm);
            AddExecutor(ExecutorType.Activate, _CardId.HarpiesFeatherDuster, DefaultHarpiesFeatherDusterFirst);
            AddExecutor(ExecutorType.Activate, _CardId.HammerShot, DefaultHammerShot);
            AddExecutor(ExecutorType.Activate, _CardId.DarkHole, DefaultDarkHole);
            AddExecutor(ExecutorType.Activate, _CardId.Raigeki, DefaultRaigeki);
            AddExecutor(ExecutorType.Activate, _CardId.SmashingGround, DefaultSmashingGround);
            AddExecutor(ExecutorType.Activate, _CardId.PotOfDesires, DefaultPotOfDesires);
            AddExecutor(ExecutorType.Activate, _CardId.AllureofDarkness, DefaultAllureofDarkness);
            AddExecutor(ExecutorType.Activate, _CardId.DimensionalBarrier, DefaultDimensionalBarrier);
            AddExecutor(ExecutorType.Activate, _CardId.InterruptedKaijuSlumber, DefaultInterruptedKaijuSlumber);

            AddExecutor(ExecutorType.SpSummon, _CardId.JizukirutheStarDestroyingKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.GadarlatheMysteryDustKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.GamecieltheSeaTurtleKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.RadiantheMultidimensionalKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.KumongoustheStickyStringKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.ThunderKingtheLightningstrikeKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.DogorantheMadFlameKaiju, DefaultKaijuSpsummon);
            AddExecutor(ExecutorType.SpSummon, _CardId.SuperAntiKaijuWarMachineMechaDogoran, DefaultKaijuSpsummon);

            AddExecutor(ExecutorType.SpSummon, _CardId.EvilswarmExcitonKnight, DefaultEvilswarmExcitonKnightSummon);
            AddExecutor(ExecutorType.Activate, _CardId.EvilswarmExcitonKnight, DefaultEvilswarmExcitonKnightEffect);

            AddExecutor(ExecutorType.Summon, _CardId.SandaionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.GabrionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.MichionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.ZaphionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.HailonTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.RaphionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.SadionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.MetaionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.KamionTheTimelord, DefaultTimelordSummon);
            AddExecutor(ExecutorType.Summon, _CardId.LazionTheTimelord, DefaultTimelordSummon);

            // Exodia pieces are worth more in the hand.
            AddExecutor(ExecutorType.Summon, _CardId.LeftArmofTheForbiddenOne, Never);
            AddExecutor(ExecutorType.Summon, _CardId.RightLegofTheForbiddenOne, Never);
            AddExecutor(ExecutorType.Summon, _CardId.LeftLegofTheForbiddenOne, Never);
            AddExecutor(ExecutorType.Summon, _CardId.RightArmofTheForbiddenOne, Never);
            AddExecutor(ExecutorType.Summon, _CardId.ExodiaTheForbiddenOne, Never);

            // Everything else, most valuable plays first. The game asks again after every
            // action, so this order is the order the bot tries things in.
            AddExecutor(ExecutorType.Activate, ActivateForCards);
            AddExecutor(ExecutorType.SpSummon, SmartSpSummon);
            AddExecutor(ExecutorType.Activate, ActivateGeneral);
            AddExecutor(ExecutorType.SummonOrSet, SmartSummon);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
        }

        // -----------------------------------------------------------------------------
        // What a card does, read from its text.

        private class Profile
        {
            public bool SpecialSummon;
            public bool NegatesResponse;   // negates an activation, Summon or attack: only worth it against the opponent
            public bool Draw;
            public bool Search;
            public bool MassSymmetric;     // hits both players ("destroy all monsters on the field")
            public bool MassOpponent;      // hits only the opponent ("destroy all monsters your opponent controls")
            public bool MassMonsters;      // the mass effect covers monsters
            public bool MassSpells;        // the mass effect covers Spells/Traps
            public bool Removal;           // destroys, banishes, bounces, negates or changes cards
            public bool Burn;
            public bool Buff;
            public bool Debuff;            // lowers ATK, stops attacks, takes control...
            public bool SelfCost;          // Tributes, sends or banishes itself as a cost
            public int LpCost;

            public bool Harmful { get { return Removal || Debuff || MassOpponent; } }
            public bool Helpful { get { return Buff || SpecialSummon || Search || Draw; } }
        }

        private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private static readonly Regex SsWord = new Regex(@"special summon", Rx);
        private static readonly Regex SsNotAnEffect = new Regex(@"(cannot be|must (first )?be|can only be) special summoned|(cannot|can't) special summon", Rx);
        private static readonly Regex NegatesResponseRx = new Regex(@"negate (the |that |its |this )?(activation|summon|special summon|normal summon|attack)\b|negate that effect|negate that card's effect|negate the effect\b(?! of)", Rx);
        private static readonly Regex DrawRx = new Regex(@"\bdraw (\d+|a|an|one|two|three)\b|\bdraw cards?\b", Rx);
        private static readonly Regex SearchRx = new Regex(@"\badd\b[^.;]*\bto your hand", Rx);
        private static readonly Regex MassRx = new Regex(@"\b(destroy|banish|send|return|shuffle)\b[^.;:]*\ball\b[^.;:]*", Rx);
        private static readonly Regex OpponentOnlyRx = new Regex(@"your opponent controls|your opponent's|opponent's (side|field)", Rx);
        private static readonly Regex BothSidesRx = new Regex(@"on the field|\ball (other )?(monsters|cards|spell)", Rx);
        private static readonly Regex RemovalVerbRx = new Regex(@"\b(destroy|banish|return|shuffle|send|negate|change)\b", Rx);
        private static readonly Regex RemovalTargetRx = new Regex(@"\b(target|on the field|your opponent|opponent's|they control)\b", Rx);
        private static readonly Regex LpCostRx = new Regex(@"pay (\d+) (lp|life points)", Rx);
        private static readonly Regex BurnRx = new Regex(@"inflict[^.;]*damage to your opponent|inflict \d+ damage", Rx);
        private static readonly Regex BuffRx = new Regex(@"gains? [^.;]{0,30}\b(atk|def)\b|(atk|def) (is )?increased", Rx);
        private static readonly Regex DebuffRx = new Regex(@"loses? (\d+|atk)|cannot attack|cannot declare an attack|face-down defense position|take control|gain control", Rx);
        private static readonly Regex SelfCostRx = new Regex(@"\b(tribute|send|banish) this card( from the field)?\b[^.;]*;", Rx);

        // What a removal effect that picks out cards one at a time can reach.
        private class Aim
        {
            public bool Monsters, Spells, Hand, Grave;
            public bool FaceUpOnly, FaceDownOnly;
            public bool Targets;   // "target": protected cards don't count
        }

        private static readonly Regex AimVerbRx = new Regex(@"\b(destroy|banish|return|shuffle|send|negate|change|place|take control|gain control)\b", Rx);
        private static readonly Regex AimObjectRx = new Regex(@"\b(\d+|a|an|one|two|three|up to \w+)\s+((face-up|face-down|set|attack position|defense position|other|effect)\s+)*(spell/traps?|spells?/traps?|spell or trap|monsters?|cards?|spells?|traps?)( cards?)?\b[^.;]*?\b(on the field|your opponent controls|they control|your opponent's field)", Rx);
        private static readonly Regex AimOpponentRx = new Regex(@"your opponent's\s+((face-up|face-down|set)\s+)?(spell/traps?|monsters?|cards?|spells?|traps?|hand|gy|graveyard)", Rx);
        private static readonly Regex HandWordRx = new Regex(@"\bhand\b", Rx);
        private static readonly Regex GraveWordRx = new Regex(@"\b(gy|graveyard)\b", Rx);
        private static readonly Regex SetWordRx = new Regex(@"\bset\b", Rx);
        private static readonly Regex YouCanRx = new Regex(@"\byou can\b", Rx);

        private static readonly Dictionary<int, Aim> CardAims = new Dictionary<int, Aim>();
        private static readonly Dictionary<int, Aim> EffectAims = new Dictionary<int, Aim>();

        private static readonly Dictionary<int, Profile> Profiles = new Dictionary<int, Profile>();
        private static readonly Profile NoProfile = new Profile();

        private static Profile GetProfile(int id)
        {
            if (id <= 0)
                return NoProfile;
            lock (Profiles)
            {
                Profile p;
                if (Profiles.TryGetValue(id, out p))
                    return p;
                NamedCard data = NamedCard.Get(id);
                p = (data == null || string.IsNullOrEmpty(data.Description)) ? NoProfile : ReadProfile(data.Description);
                Profiles[id] = p;
                return p;
            }
        }

        private static Profile ReadProfile(string description)
        {
            string text = description.Replace('\r', ' ').Replace('\n', ' ');
            Profile p = new Profile();

            foreach (Match m in SsWord.Matches(text))
            {
                int start = Math.Max(0, m.Index - 30);
                int end = Math.Min(text.Length, m.Index + m.Length + 5);
                if (!SsNotAnEffect.IsMatch(text.Substring(start, end - start)))
                {
                    p.SpecialSummon = true;
                    break;
                }
            }
            p.NegatesResponse = NegatesResponseRx.IsMatch(text);
            p.Draw = DrawRx.IsMatch(text);
            p.Search = SearchRx.IsMatch(text);

            foreach (Match m in MassRx.Matches(text))
            {
                string clause = m.Value.ToLowerInvariant();
                bool hit = false;
                if (OpponentOnlyRx.IsMatch(clause))
                {
                    p.MassOpponent = true;
                    hit = true;
                }
                else if (BothSidesRx.IsMatch(clause))
                {
                    p.MassSymmetric = true;
                    hit = true;
                }
                if (hit)
                {
                    bool spellsNamed = clause.Contains("spell") || clause.Contains("trap");
                    bool monstersNamed = clause.Contains("monster");
                    if (monstersNamed || clause.Contains("cards") && !spellsNamed)
                        p.MassMonsters = true;   // "all monsters", or "all cards"
                    if (spellsNamed || clause.Contains("cards") && !monstersNamed)
                        p.MassSpells = true;     // "all Spell and Trap Cards", or "all cards"
                }
            }
            if ((p.MassSymmetric || p.MassOpponent) && !p.MassMonsters && !p.MassSpells)
                p.MassMonsters = true;

            foreach (string sentence in text.Split('.'))
            {
                if (RemovalVerbRx.IsMatch(sentence) && RemovalTargetRx.IsMatch(sentence))
                {
                    p.Removal = true;
                    break;
                }
            }

            Match lp = LpCostRx.Match(text);
            if (lp.Success)
            {
                int cost;
                if (int.TryParse(lp.Groups[1].Value, out cost))
                    p.LpCost = cost;
            }
            p.Burn = BurnRx.IsMatch(text);
            p.Buff = BuffRx.IsMatch(text);
            p.Debuff = DebuffRx.IsMatch(text);
            p.SelfCost = SelfCostRx.IsMatch(text);
            return p;
        }

        // Reads which of the opponent's cards a "pick 1 card" removal effect can reach, e.g.
        // "Target 1 Spell/Trap on the field; destroy it". Null if the text has none.
        private static Aim ReadAim(string description)
        {
            if (string.IsNullOrEmpty(description))
                return null;
            string text = description.Replace('\r', ' ').Replace('\n', ' ');
            Aim aim = null;
            foreach (string sentence in text.Split('.'))
            {
                if (!AimVerbRx.IsMatch(sentence))
                    continue;
                List<Match> phrases = AimObjectRx.Matches(sentence).Cast<Match>()
                    .Concat(AimOpponentRx.Matches(sentence).Cast<Match>()).ToList();
                foreach (Match phrase in phrases)
                {
                    string words = phrase.Value.ToLowerInvariant();
                    if (aim == null)
                        aim = new Aim();
                    bool zoneNamed = false;
                    if (words.Contains("monster"))
                    {
                        aim.Monsters = true;
                        zoneNamed = true;
                    }
                    if (words.Contains("spell") || words.Contains("trap"))
                    {
                        aim.Spells = true;
                        zoneNamed = true;
                    }
                    if (HandWordRx.IsMatch(words))
                    {
                        aim.Hand = true;
                        zoneNamed = true;
                    }
                    if (GraveWordRx.IsMatch(words))
                    {
                        aim.Grave = true;
                        zoneNamed = true;
                    }
                    if (!zoneNamed)
                    {
                        aim.Monsters = true;   // "1 card": any card on the field
                        aim.Spells = true;
                    }
                    if (words.Contains("face-up"))
                        aim.FaceUpOnly = true;
                    else if (words.Contains("face-down") || SetWordRx.IsMatch(words))
                        aim.FaceDownOnly = true;
                    if (sentence.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0)
                        aim.Targets = true;
                }
            }
            if (aim != null && aim.FaceUpOnly && aim.FaceDownOnly)
            {
                aim.FaceUpOnly = false;   // different phrases disagree: don't restrict
                aim.FaceDownOnly = false;
            }
            return aim;
        }

        private static bool HasSeveralEffects(string text)
        {
            return text.Contains("(2)") || text.Contains("\u25CF") || YouCanRx.Matches(text).Count >= 2;
        }

        // -----------------------------------------------------------------------------
        // Card details that work wherever the card is (deck, hand, field, GY...).

        private static int TypeOf(ClientCard card)
        {
            if (card.Type != 0)
                return card.Type;
            return card.Data != null ? card.Data.Type : 0;
        }

        private static bool Is(ClientCard card, CardType type)
        {
            return (TypeOf(card) & (int)type) != 0;
        }

        private static bool IsExtra(ClientCard card)
        {
            return Is(card, CardType.Fusion) || Is(card, CardType.Synchro) || Is(card, CardType.Xyz) || Is(card, CardType.Link);
        }

        private static bool Known(ClientCard card)
        {
            return card.Id != 0 && card.Data != null;
        }

        private static int Atk(ClientCard card)
        {
            if (card.Location == CardLocation.MonsterZone && card.Id != 0)
                return Math.Max(0, card.Attack);
            if (card.Data != null)
                return Math.Max(0, card.Data.Attack);
            return Math.Max(0, card.Attack);
        }

        private static int Def(ClientCard card)
        {
            if (card.Location == CardLocation.MonsterZone && card.Id != 0)
                return Math.Max(0, card.Defense);
            if (card.Data != null)
                return Math.Max(0, card.Data.Defense);
            return Math.Max(0, card.Defense);
        }

        private static int Lvl(ClientCard card)
        {
            if (card.Level > 0)
                return card.Level;
            return card.Data != null ? card.Data.Level : 0;
        }

        // Rough worth of a card to whoever has it.
        private static int Value(ClientCard card)
        {
            if (card == null)
                return 0;
            bool onBoard = card.Location == CardLocation.MonsterZone || card.Location == CardLocation.SpellZone;
            if (!Known(card))
                return card.Location == CardLocation.SpellZone ? 1500 : 1300;   // face-down: assume something decent
            if (Is(card, CardType.Monster))
            {
                if (Is(card, CardType.Token))
                    return 200 + Atk(card) / 4;
                int power = Atk(card);
                if (card.Location == CardLocation.MonsterZone && card.IsDefense())
                    power = Math.Max(power, Def(card));
                int value = power + Lvl(card) * 30;
                if (IsExtra(card))
                    value += 400;
                if (Is(card, CardType.Effect))
                    value += 250;
                if (onBoard)
                    value += 300;
                return value;
            }
            if (Is(card, CardType.Field))
                return 1300;
            if (Is(card, CardType.Continuous) || Is(card, CardType.Equip))
                return 1400;
            return 1500;
        }

        // How much we want an opponent's card gone.
        private static int Threat(ClientCard card)
        {
            int value = Value(card);
            if (card.Location == CardLocation.MonsterZone && card.IsFaceup())
            {
                if (card.IsFloodgate() || card.IsMonsterDangerous() || card.IsMonsterInvincible())
                    value += 3000;
                if (card.IsAttack())
                    value += 200;
            }
            else if (card.Location == CardLocation.SpellZone)
            {
                if (card.IsFaceup() && card.IsFloodgate())
                    value += 3000;
                if (card.IsFacedown())
                    value += 100;
            }
            if (card.IsShouldNotBeTarget())
                value -= 2000;
            return value;
        }

        // -----------------------------------------------------------------------------
        // Activating cards and effects.

        private ClientCard _lastActivated;

        private bool Never()
        {
            return false;
        }

        // Card draw and searches come first: more options for the rest of the turn.
        private bool ActivateForCards()
        {
            Profile p = GetProfile(Card.Id);
            if (!p.Draw && !p.Search)
                return false;
            return ShouldActivate(p);
        }

        private bool ActivateGeneral()
        {
            return ShouldActivate(GetProfile(Card.Id));
        }

        private bool HasSpecificExecutor(params ExecutorType[] types)
        {
            return Executors.Any(exec => exec.CardId != -1 && types.Contains(exec.Type) && Card.IsOriginalCode(exec.CardId));
        }

        private bool OpponentIsActing()
        {
            if (Duel.LastChainPlayer == 1)
                return true;
            if (Duel.CurrentChain.Count == 0 && Duel.LastSummonPlayer == 1)
                return true;
            return Duel.Player == 1 && Bot.UnderAttack;
        }

        private bool ShouldActivate(Profile p)
        {
            if (HasSpecificExecutor(ExecutorType.Activate))
                return false;   // a staple with its own logic above
            if (DefaultCheckWhetherCardIsNegated(Card))
                return false;

            bool facedownSpellTrap = Card.Location == CardLocation.SpellZone && Card.IsFacedown();
            bool spellTrap = Is(Card, CardType.Spell) || Is(Card, CardType.Trap) || facedownSpellTrap;

            // Don't stack our own Spells/Traps on our own chain (monster triggers may still chain).
            if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 0 && spellTrap)
                return false;

            if (p.LpCost > 0 && Bot.LifePoints - p.LpCost < (OpponentIsActing() ? 1 : 1000))
                return false;   // keep a safety margin unless it's to stop the opponent
            if (p.Draw && !p.Search && Bot.Deck.Count <= 3)
                return false;   // don't draw ourselves out of cards
            if (p.NegatesResponse && !OpponentIsActing())
                return false;   // never negate our own plays
            if (p.MassSymmetric && !WipeHelpsUs(p))
                return false;
            if (p.MassOpponent && !OpponentHasCardsFor(p))
                return false;

            // Removal that picks out cards one at a time: only worth it if the opponent has
            // something it can hit. Otherwise the only legal targets are our own cards.
            Aim aim = AimOfActivation();
            if (aim != null && !OpponentHasTargetFor(aim))
                return false;

            if (facedownSpellTrap && !GoodTimeForSetCard(p))
                return false;

            if (Card.Location == CardLocation.Hand && Is(Card, CardType.Field))
            {
                ClientCard field = Bot.SpellZone[5];
                if (field != null && field.IsFaceup())
                    return false;   // keep the Field Spell we already have
            }

            if (Is(Card, CardType.Equip) && (Card.Location == CardLocation.Hand || facedownSpellTrap))
            {
                // Harmful equips go on the opponent's monsters, helpful ones on ours.
                if (p.Debuff ? Enemy.GetFaceupMonsters().Count == 0 : Bot.GetFaceupMonsters().Count == 0)
                    return false;
            }

            if (Card.Location == CardLocation.Hand && Is(Card, CardType.Pendulum) && Is(Card, CardType.Monster))
            {
                // Placing a Pendulum Scale: worth it once we have the other scale, or for a weak monster.
                ClientCard left = Util.GetPZone(0, 0), right = Util.GetPZone(0, 1);
                bool haveScale = left != null && Is(left, CardType.Pendulum) || right != null && Is(right, CardType.Pendulum);
                if (!haveScale && Atk(Card) > 1500)
                    return false;
            }

            if (p.SelfCost && Card.Location == CardLocation.MonsterZone && Duel.Player == 0
                && Duel.Phase == DuelPhase.Main1 && Card.IsAttack() && !Card.Attacked && CanAttackWell(Card))
                return false;   // attack first, use the effect in Main Phase 2

            _lastActivated = Card;
            return true;
        }

        // When to flip a set Spell/Trap.
        private bool GoodTimeForSetCard(Profile p)
        {
            if (Duel.CurrentChain.Count > 0)
                return Duel.LastChainPlayer == 1;   // in answer to the opponent

            if (Duel.Player == 1)
            {
                if (Duel.LastSummonPlayer == 1 || Bot.UnderAttack)
                    return true;
                if (Duel.Phase == DuelPhase.End)
                    return true;   // last chance before our turn
                if (Duel.Phase == DuelPhase.BattleStart && (p.Removal || p.MassOpponent) && Enemy.GetMonsters().Any(m => m.IsAttack()))
                    return true;
                return false;
            }

            // Our turn.
            if (Duel.Phase == DuelPhase.Main1 || Duel.Phase == DuelPhase.Main2)
            {
                if (p.SpecialSummon || p.Draw || p.Search || p.Burn)
                    return true;
                if (Duel.Phase == DuelPhase.Main1 && (p.Removal || p.MassOpponent) && Enemy.GetFieldCount() > 0)
                    return true;   // clear the way before attacking
                return false;
            }
            return p.Buff && Bot.BattlingMonster != null;   // boost our monster mid-battle
        }

        private bool WipeHelpsUs(Profile p)
        {
            int ours = 0, theirs = 0;
            if (p.MassMonsters)
            {
                ours += Bot.GetMonsters().Sum(m => Value(m));
                theirs += Enemy.GetMonsters().Sum(m => Threat(m));
            }
            if (p.MassSpells)
            {
                ours += Bot.GetSpells().Where(s => s != Card).Sum(s => Value(s));
                theirs += Enemy.GetSpells().Sum(s => Threat(s));
            }
            if (theirs <= 0)
                return false;
            if (ours == 0 || theirs >= ours + 1000)
                return true;
            // About to lose to their attackers anyway? Then any trade that hurts them more helps.
            return theirs > ours && Util.GetTotalAttackingMonsterAttack(1) >= Bot.LifePoints;
        }

        private bool OpponentHasCardsFor(Profile p)
        {
            if (p.MassMonsters && Enemy.GetMonsters().Count > 0)
                return true;
            return p.MassSpells && Enemy.GetSpells().Count > 0;
        }

        // What the effect being activated can hit, if it's "pick 1 card" removal. Null when it
        // isn't, or when the card has several effects and we can't tell which one this is.
        private Aim AimOfActivation()
        {
            int desc = ActivateDescription;
            int code = desc > 0 ? desc >> 4 : 0;
            if (code > 0 && (code == Card.Id || code == Card.Alias))
            {
                // The effect's own description, when the card has one.
                Aim fromEffect;
                lock (Profiles)
                {
                    if (!EffectAims.TryGetValue(desc, out fromEffect))
                    {
                        fromEffect = ReadAim(OptionText(desc));
                        EffectAims[desc] = fromEffect;
                    }
                }
                if (fromEffect != null)
                    return fromEffect;
            }
            if (Card.Data == null || string.IsNullOrEmpty(Card.Data.Description))
                return null;
            // Activating a Spell/Trap card uses its main text; otherwise only trust the
            // whole text when the card has a single effect.
            bool activatingSpellTrap = (Is(Card, CardType.Spell) || Is(Card, CardType.Trap))
                && (Card.Location == CardLocation.Hand || Card.Location == CardLocation.SpellZone && Card.IsFacedown());
            if (!activatingSpellTrap && HasSeveralEffects(Card.Data.Description))
                return null;
            Aim fromCard;
            lock (Profiles)
            {
                if (!CardAims.TryGetValue(Card.Id, out fromCard))
                {
                    fromCard = ReadAim(Card.Data.Description);
                    CardAims[Card.Id] = fromCard;
                }
            }
            return fromCard;
        }

        private bool OpponentHasTargetFor(Aim aim)
        {
            Func<ClientCard, bool> fits = c => c != null
                && (!aim.FaceUpOnly || c.IsFaceup())
                && (!aim.FaceDownOnly || c.IsFacedown())
                && (!aim.Targets || !c.IsShouldNotBeTarget());
            if (aim.Monsters && Enemy.GetMonsters().Any(fits))
                return true;
            if (aim.Spells && Enemy.GetSpells().Any(fits))
                return true;
            if (aim.Hand && Enemy.Hand.Count > 0)
                return true;
            return aim.Grave && Enemy.Graveyard.Count > 0;
        }

        private bool CanAttackWell(ClientCard card)
        {
            int best = Util.GetBestPower(Enemy);
            return best < 0 || Atk(card) > best;
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            _lastActivated = null;
        }

        public override void OnNewPhase()
        {
            base.OnNewPhase();
            _lastActivated = null;
        }

        // -----------------------------------------------------------------------------
        // Summoning.

        private bool SmartSpSummon()
        {
            if (HasSpecificExecutor(ExecutorType.SpSummon))
                return false;
            if (Card.Location == CardLocation.Extra || IsExtra(Card))
                return ExtraDeckUpgrade(Card);
            return true;   // from the hand or GY, or a Pendulum Summon: a free body
        }

        // An Extra Deck monster is worth it if it beats the monsters it would replace.
        // Materials are picked weakest first, so compare with our weakest monsters.
        private bool ExtraDeckUpgrade(ClientCard card)
        {
            List<ClientCard> mine = Bot.GetMonsters().OrderBy(m => Value(m)).ToList();
            if (mine.Count == 0)
                return true;
            Profile p = GetProfile(card.Id);
            if (p.Draw || p.Search || p.Removal || p.MassOpponent)
                return true;   // useful effect
            int needed = Is(card, CardType.Link) ? Math.Max(1, card.LinkCount > 0 ? card.LinkCount : Lvl(card)) : 2;
            ClientCard strongestUsed = mine[Math.Min(needed, mine.Count) - 1];
            return Atk(card) >= Atk(strongestUsed);
        }

        private bool SmartSummon()
        {
            if (HasSpecificExecutor(ExecutorType.Summon, ExecutorType.SummonOrSet, ExecutorType.MonsterSet))
                return false;
            if (!WorthNormalSummoning(Card))
                return false;
            // Only the best monster in hand gets the Normal Summon.
            ClientCard best = null;
            int bestScore = int.MinValue;
            foreach (ClientCard candidate in Duel.MainPhase.SummonableCards)
            {
                if (!WorthNormalSummoning(candidate))
                    continue;
                if (Executors.Any(exec => exec.CardId != -1 && candidate.IsOriginalCode(exec.CardId)
                    && (exec.Type == ExecutorType.Summon || exec.Type == ExecutorType.SummonOrSet || exec.Type == ExecutorType.MonsterSet)))
                    continue;
                int score = SummonScore(candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best == null || best == Card;
        }

        private bool WorthNormalSummoning(ClientCard card)
        {
            int level = Lvl(card);
            if (level <= 4)
                return true;
            int tributes = level >= 7 ? 2 : 1;
            int newPower = Math.Max(Atk(card), Def(card));
            // Only Tribute monsters that are weaker than what we'd get.
            int weaker = Bot.GetMonsters().Count(m => m != card && Math.Max(Atk(m), Def(m)) < newPower);
            return weaker >= tributes;
        }

        private int SummonScore(ClientCard card)
        {
            int score = Math.Max(Atk(card), Def(card) * 3 / 4) + Lvl(card) * 10;
            if (Is(card, CardType.Effect))
                score += 150;
            if (Is(card, CardType.Flip))
                score += 150;
            return score;
        }

        // True = Set it face-down instead of Normal Summoning it.
        public override bool OnSelectMonsterSummonOrSet(ClientCard card)
        {
            if (Is(card, CardType.Flip))
                return true;
            int enemyBest = Util.GetBestPower(Enemy);
            if (enemyBest < 0)
                return false;   // nothing to hit us back: attack
            return Atk(card) < enemyBest;
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            if (AI.HaveSelectedPosition())
                return 0;
            NamedCard data = NamedCard.Get(cardId);
            if (data == null)
                return 0;
            int atk = data.Attack, def = data.Defense;
            CardPosition wanted;
            if (atk < 0)
                wanted = CardPosition.FaceUpAttack;   // "?" ATK: usually set by an effect
            else
            {
                int enemyBest = Util.GetBestPower(Enemy);
                bool canFight = enemyBest < 0 ? atk > 0 : atk >= enemyBest;
                wanted = (canFight || atk >= def && atk >= 2000) ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence;
            }
            return positions.Contains(wanted) ? wanted : (CardPosition)0;
        }

        // -----------------------------------------------------------------------------
        // Battle: an unknown face-down monster could have a solid DEF, so don't throw
        // weak monsters at it.

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!base.OnPreBattleBetween(attacker, defender))
                return false;
            if (defender.IsFacedown() && defender.Id == 0 && defender.RealPower < 1300)
                defender.RealPower = 1300;
            return true;
        }

        // -----------------------------------------------------------------------------
        // Choosing cards: targets, costs, materials, cards to add or Summon.

        private enum Intent { Unknown, Harm, Gain, Material }

        private static Intent IntentOfHint(int hint)
        {
            switch (hint)
            {
                case HintMsg.AddToHand:
                case HintMsg.Summon:
                case HintMsg.SpSummon:
                case HintMsg.Set:
                case HintMsg.ToField:
                    return Intent.Gain;
                case HintMsg.Release:
                case HintMsg.Discard:
                case HintMsg.Destroy:
                case HintMsg.Remove:
                case HintMsg.ToGrave:
                case HintMsg.ReturnToHand:
                case HintMsg.ToDeck:
                case HintMsg.Facedown:
                case HintMsg.RemoveXyz:
                case HintMsg.Control:
                case HintMsg.DestroyReplace:
                case HintMsg.Tribute:
                case HintMsg.DeattachFrom:
                case HintMsg.Disable:
                    return Intent.Harm;
                case HintMsg.FusionMaterial:
                case HintMsg.SynchroMaterial:
                case HintMsg.XyzMaterial:
                case HintMsg.LinkMaterial:
                    return Intent.Material;
                default:
                    return Intent.Unknown;
            }
        }

        // The card whose effect is asking for the selection.
        private int SourceCardId()
        {
            ChainInfo solving = Duel.GetCurrentSolvingChainInfo();
            if (solving != null)
                return solving.ActivateId;
            if (Duel.CurrentChain.Count > 0)
            {
                ClientCard last = Duel.CurrentChain[Duel.CurrentChain.Count - 1];
                if (last != null)
                    return last.Id;
            }
            return _lastActivated != null ? _lastActivated.Id : 0;
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            IList<ClientCard> special = base.OnSelectCard(cards, min, max, hint, cancelable);
            if (special != null)
                return special;
            if (AI.HaveSelectedCards() || hint == HintMsg.AttackTarget || cards.Count == 0)
                return null;
            if (max > cards.Count)
                max = cards.Count;
            if (min > max)
                return null;

            Intent intent = IntentOfHint(hint);
            if (intent == Intent.Unknown)
            {
                Profile source = GetProfile(SourceCardId());
                bool opponentCardOnField = cards.Any(c => c.Controller == 1 && (c.Location == CardLocation.MonsterZone || c.Location == CardLocation.SpellZone));
                if (source.Harmful && (opponentCardOnField || !source.Helpful))
                    intent = Intent.Harm;
                else if (source.Helpful)
                    intent = Intent.Gain;
                else
                    return null;
            }

            List<ClientCard> picked = new List<ClientCard>();
            if (intent == Intent.Material)
            {
                // Our least valuable cards, as few as allowed.
                foreach (ClientCard card in cards.OrderBy(c => c.Controller == 0 ? Value(c) : Value(c) + 5000))
                {
                    if (picked.Count >= min)
                        break;
                    picked.Add(card);
                }
                return picked;
            }

            if (intent == Intent.Harm)
            {
                bool fromOwnDeck = cards.All(c => c.Controller == 0 && (c.Location == CardLocation.Deck || c.Location == CardLocation.Extra));
                if (fromOwnDeck)
                {
                    // e.g. sending a monster from the Deck to the GY: pick the strongest, for later revival.
                    foreach (ClientCard card in cards.OrderByDescending(c => Value(c)))
                    {
                        if (picked.Count >= Math.Max(min, 1) || picked.Count >= max)
                            break;
                        picked.Add(card);
                    }
                    return picked;
                }
                // The opponent's most threatening cards, as many as allowed...
                foreach (ClientCard card in cards.Where(c => c.Controller == 1).OrderByDescending(c => Threat(c)))
                {
                    if (picked.Count >= max)
                        break;
                    picked.Add(card);
                }
                // ...and only as many of our own (least valuable first) as we must.
                foreach (ClientCard card in cards.Where(c => c.Controller != 1).OrderBy(c => Value(c)))
                {
                    if (picked.Count >= min)
                        break;
                    picked.Add(card);
                }
                return picked;
            }

            // Gain: the best cards, as many as allowed. Prefer our own cards (e.g. for boosts),
            // except when Special Summoning, where the opponent's GY counts too.
            List<ClientCard> pool = cards.ToList();
            if (hint != HintMsg.SpSummon)
            {
                List<ClientCard> own = cards.Where(c => c.Controller == 0).ToList();
                if (own.Count >= Math.Max(min, 1))
                    pool = own;
            }
            bool takeMax = hint == HintMsg.SpSummon || hint == HintMsg.AddToHand || hint == HintMsg.Set || hint == HintMsg.ToField;
            int count = takeMax ? max : Math.Max(min, Math.Min(1, max));
            foreach (ClientCard card in pool.OrderByDescending(c => Value(c)))
            {
                if (picked.Count >= count)
                    break;
                picked.Add(card);
            }
            foreach (ClientCard card in cards.OrderByDescending(c => Value(c)))
            {
                if (picked.Count >= min)
                    break;
                if (!picked.Contains(card))
                    picked.Add(card);
            }
            return picked;
        }

        // -----------------------------------------------------------------------------
        // Choosing between a card's effects: prefer the one whose text sounds most useful.

        private static Dictionary<int, string[]> _cardStrings;

        private static string FindDatabase()
        {
            string name = Config.GetString("DbPath", "cards.cdb");
            string[] candidates = { name, "../" + name, "../cdb/" + name };
            foreach (string candidate in candidates)
            {
                string full = Path.GetFullPath(candidate);
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        private static string OptionText(int desc)
        {
            int code = desc >> 4;
            int index = desc & 0xf;
            if (code <= 0)
                return null;
            lock (Profiles)
            {
                if (_cardStrings == null)
                {
                    _cardStrings = new Dictionary<int, string[]>();
                    try
                    {
                        string path = FindDatabase();
                        if (path != null)
                        {
                            using (SqliteConnection connection = new SqliteConnection("Data Source=" + path))
                            {
                                connection.Open();
                                using (IDbCommand command = new SqliteCommand(
                                    "SELECT id, str1, str2, str3, str4, str5, str6, str7, str8, str9, str10, str11, str12, str13, str14, str15, str16 FROM texts",
                                    connection))
                                {
                                    using (IDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            string[] strings = new string[16];
                                            for (int i = 0; i < 16; ++i)
                                                strings[i] = reader.IsDBNull(i + 1) ? "" : reader.GetString(i + 1);
                                            _cardStrings[reader.GetInt32(0)] = strings;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteErrorLine("Smart AI: couldn't read the effect names from the card database: " + ex.Message);
                    }
                }
                string[] found;
                if (_cardStrings.TryGetValue(code, out found) && index < found.Length)
                    return found[index];
            }
            return null;
        }

        private int ScoreOption(string text)
        {
            string t = text.ToLowerInvariant();
            int score = 0;
            if (t.Contains("special summon")) score += 30;
            if (t.Contains("draw")) score += 30;
            if (t.Contains("add")) score += 25;
            if ((t.Contains("destroy") || t.Contains("banish")) && Enemy.GetFieldCount() > 0) score += 20;
            if (t.Contains("damage")) score += 15;
            if (t.Contains("gain") || t.Contains("increase")) score += 10;
            if (t.Contains("pay") || t.Contains("discard") || t.Contains("lose")) score -= 20;
            if (t.Contains("your opponent draws") || t.Contains("your opponent gains") || t.Contains("your opponent adds")) score -= 40;
            return score;
        }

        public override int OnSelectOption(IList<int> options)
        {
            // Leave choices to a staple's own logic.
            ClientCard source = Duel.GetCurrentSolvingChainCard() ?? Duel.GetCurrentChainCard();
            if (source != null && Executors.Any(exec => exec.CardId != -1 && source.IsOriginalCode(exec.CardId)))
                return -1;

            int best = -1, bestScore = 0, ties = 0;
            for (int i = 0; i < options.Count; ++i)
            {
                string text = OptionText(options[i]);
                if (string.IsNullOrEmpty(text))
                    continue;
                int score = ScoreOption(text);
                if (best < 0 || score > bestScore)
                {
                    best = i;
                    bestScore = score;
                    ties = 1;
                }
                else if (score == bestScore)
                {
                    ties++;
                }
            }
            if (best >= 0 && ties == 1)
                return best;
            return Program.Rand.Next(options.Count);
        }
    }
}
