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
    //  - removal that picks out cards one at a time aims at the opponent's cards; it
    //    only hits our own when that card gains from it, or to save our card from (or
    //    spoil) an opponent's effect aimed at it (see "Removal aimed at single cards")
    //  - set Spells/Traps are kept for the opponent's turn (their summons, attacks,
    //    or End Phase) unless they help us right now
    //  - negation only against the opponent, never against our own plays
    //  - removal is aimed at the opponent's most threatening card; costs, discards
    //    and materials use our least valuable cards
    //  - attacks follow a battle plan (lethal first, then the most threatening monsters,
    //    weakest attacker that wins each fight); in Main Phase 1 monsters with a part
    //    in that plan go to Attack Position, and others only when their ATK beats both
    //    their own DEF and every opposing monster left standing
    //  - FLIP monsters are only flipped when their effect has a use and a valid target
    //  - no effects that cost more LP than we have
    [Deck("Smart", "AI_Test", "Test")]
    public class SmartExecutor : DefaultExecutor
    {
        public SmartExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            // Card-specific logic WindBot already has for common staples. (Mystical Space
            // Typhoon, Cosmic Cyclone and Galaxy Cyclone use our own removal rules instead.)
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
            AddExecutor(ExecutorType.Repos, SmartRepos);
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

        // How a removal effect gets rid of a card.
        private enum RemovalKind { Other, Destroy, Banish, ToHand, ToDeck, ToGrave }

        // What a removal effect that picks out cards one at a time can reach.
        private class Aim
        {
            public bool Monsters, Spells, Hand, Grave;
            public bool FaceUpOnly, FaceDownOnly;
            public bool Targets;        // "target": protected cards don't count
            public bool EitherSide;     // "on the field": it can also hit our own cards
            public bool OwnRequired;    // "1 monster you control and 1 ...": it hits one of ours anyway
            public int MinTargets = 1;  // "Target 2 cards": 2 ("up to 2": 1)
            public RemovalKind Kind;    // destroy, banish, return to the hand...
            public bool FollowUp;       // more happens after the removal ("then", "and if you do")
            public bool Temporary;      // banished only until it comes back ("until the End Phase")
            public bool StopsAttack;    // "this card cannot attack" the turn it's used
        }

        private static readonly Regex AimVerbRx = new Regex(@"\b(destroy|banish|return|shuffle|send|negate|change|place|take control|gain control)\b", Rx);
        private static readonly Regex AimObjectRx = new Regex(@"\b(?<n>\d+|a|an|one|two|three|up to \w+)\s+((face-up|face-down|set|attack position|defense position|other|effect)\s+)*(spell/traps?|spells?/traps?|spell or trap|monsters?|cards?|spells?|traps?)( cards?)?\b[^.;:]*?\b(?<side>on the field|your opponent controls|they control|your opponent's field)", Rx);
        private static readonly Regex AllWordRx = new Regex(@"\ball\b", Rx);
        private static readonly Regex FollowUpRx = new Regex(@"\bthen\b|\bif you do\b|\bafter that\b", Rx);
        private static readonly Regex TemporaryRx = new Regex(@"\buntil the (end|standby) phase\b|\breturn (it|them|that (card|monster|target)|those (cards|monsters)) to the field\b", Rx);
        private static readonly Regex StopsAttackRx = new Regex(@"\bthis card cannot (attack|declare an attack)\b", Rx);
        private static readonly Regex ClauseSplitRx = new Regex(@",|\bthen\b|\band if you do\b|\balso\b", Rx);
        private static readonly Regex TargetRefRx = new Regex(@"\b(it|them|that target|those targets|that card|that monster|those cards|those monsters|the target|the targets)\b", Rx);
        private static readonly KeyValuePair<Regex, RemovalKind>[] KindVerbs =
        {
            new KeyValuePair<Regex, RemovalKind>(new Regex(@"\bdestroy", Rx), RemovalKind.Destroy),
            new KeyValuePair<Regex, RemovalKind>(new Regex(@"\bbanish", Rx), RemovalKind.Banish),
            new KeyValuePair<Regex, RemovalKind>(new Regex(@"\breturn\b[^.;]*?\bto the hand\b|\bto (its|their|the) owner('s|s'|s)? hands?\b", Rx), RemovalKind.ToHand),
            new KeyValuePair<Regex, RemovalKind>(new Regex(@"\b(shuffle|return)\b[^.;]*?\bdeck\b|\bplace\b[^.;]*?\b(top|bottom) of the deck\b", Rx), RemovalKind.ToDeck),
            new KeyValuePair<Regex, RemovalKind>(new Regex(@"\bsend\b[^.;]*?\b(gy|graveyard)\b", Rx), RemovalKind.ToGrave),
        };
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

        // Reads which cards a "pick 1 card" removal effect can reach, e.g.
        // "Target 1 Spell/Trap on the field; destroy it". Null if the text has none.
        private static Aim ReadAim(string description)
        {
            if (string.IsNullOrEmpty(description))
                return null;
            string text = description.Replace('\r', ' ').Replace('\n', ' ');
            Aim aim = null;
            bool kindRead = false;
            string[] sentences = text.Split('.');
            for (int s = 0; s < sentences.Length; ++s)
            {
                string sentence = sentences[s];
                if (!AimVerbRx.IsMatch(sentence))
                    continue;
                List<Match> phrases = AimObjectRx.Matches(sentence).Cast<Match>()
                    .Concat(AimOpponentRx.Matches(sentence).Cast<Match>()).ToList();
                foreach (Match phrase in phrases)
                {
                    string words = phrase.Value.ToLowerInvariant();
                    if (AllWordRx.IsMatch(words))
                        continue;   // "destroy all monsters on the field": a mass effect, not a pick
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

                    Group side = phrase.Groups["side"];
                    if (side.Success && side.Value.Equals("on the field", StringComparison.OrdinalIgnoreCase))
                        aim.EitherSide = true;
                    if (words.Contains("you control"))
                        aim.OwnRequired = true;
                    Group count = phrase.Groups["n"];
                    if (count.Success)
                        aim.MinTargets = Math.Max(aim.MinTargets, CountWord(count.Value));

                    // What happens to the card: the part after the ";" ("Target 1 card; destroy it").
                    int semicolon = sentence.IndexOf(';');
                    string action = semicolon >= 0 ? sentence.Substring(semicolon + 1) : sentence;
                    if (!kindRead)
                    {
                        aim.Kind = KindOf(action);
                        kindRead = true;
                    }
                    if (FollowUpRx.IsMatch(action))
                        aim.FollowUp = true;
                    string next = s + 1 < sentences.Length ? sentences[s + 1] : "";
                    if (TemporaryRx.IsMatch(sentence) || TemporaryRx.IsMatch(next))
                        aim.Temporary = true;
                }
            }
            if (aim != null && StopsAttackRx.IsMatch(text))
                aim.StopsAttack = true;
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

        private static int CountWord(string word)
        {
            string w = word.Trim().ToLowerInvariant();
            if (w.StartsWith("up to"))
                return 1;
            int n;
            if (int.TryParse(w, out n))
                return Math.Max(1, n);
            if (w == "two")
                return 2;
            if (w == "three")
                return 3;
            return 1;
        }

        // How the picked card is got rid of. Looks first at the verb next to "it", "that
        // target"... ("send 1 card from your hand to the GY, and if you do, return that
        // target to the hand" returns the target to the hand).
        private static RemovalKind KindOf(string action)
        {
            foreach (string clause in ClauseSplitRx.Split(action))
            {
                if (!TargetRefRx.IsMatch(clause))
                    continue;
                RemovalKind kind = FirstKind(clause);
                if (kind != RemovalKind.Other)
                    return kind;
            }
            return FirstKind(action);
        }

        private static RemovalKind FirstKind(string text)
        {
            RemovalKind kind = RemovalKind.Other;
            int first = int.MaxValue;
            foreach (KeyValuePair<Regex, RemovalKind> verb in KindVerbs)
            {
                Match m = verb.Key.Match(text);
                if (m.Success && m.Index < first)
                {
                    first = m.Index;
                    kind = verb.Value;
                }
            }
            return kind;
        }

        // Finding which of a card's effects is being used, from the effect's name in the
        // database ("Banish", "Destroy a Set Spell/Trap"): the part of the card text that
        // shares the most words with it. Words in what the effect does (after its ":" or
        // ";") count double.
        private static readonly Regex EffectSplitRx = new Regex(@"\u25CF|\(\d+\)|\[ ?(?:Pendulum|Monster) Effect ?\]|-{5,}", Rx);
        private static readonly Regex LetterWordRx = new Regex(@"[a-z]+", RegexOptions.CultureInvariant);
        private static readonly HashSet<string> LabelStopWords = new HashSet<string>
        {
            "the", "a", "an", "of", "to", "and", "or", "this", "that", "card", "cards", "your", "from", "with",
            "for", "its", "it", "in", "on", "by", "you", "use", "effect", "effects", "activate", "up"
        };

        private static List<string> SplitEffects(string text)
        {
            string t = text.Replace('\r', ' ').Replace('\n', ' ');
            List<string> parts = EffectSplitRx.Split(t).Select(part => part.Trim()).Where(part => part.Length > 10).ToList();
            if (parts.Count <= 1)
                parts = t.Split('.').Select(part => part.Trim()).Where(part => part.Length > 10).ToList();
            return parts;
        }

        private static string Stem(string word)
        {
            foreach (string suffix in new[] { "ing", "ed", "es", "s" })
            {
                if (word.EndsWith(suffix) && word.Length - suffix.Length >= 4)
                    return word.Substring(0, word.Length - suffix.Length);
            }
            return word;
        }

        // The part of the text that the effect name is about, or null if no single part fits best.
        private static string MatchEffect(string text, string label)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(label))
                return null;
            List<string> words = LetterWordRx.Matches(label.ToLowerInvariant()).Cast<Match>()
                .Select(m => m.Value)
                .Where(w => w.Length >= 3 && !LabelStopWords.Contains(w))
                .Select(w => Stem(w))
                .Distinct().ToList();
            if (words.Count == 0)
                return null;
            List<Regex> finders = words.Select(w => new Regex(@"\b" + Regex.Escape(w), RegexOptions.CultureInvariant)).ToList();
            string best = null;
            int bestScore = 0;
            bool tie = false;
            foreach (string part in SplitEffects(text))
            {
                string lower = part.ToLowerInvariant();
                int cut = lower.IndexOf(';');
                if (cut < 0)
                    cut = lower.IndexOf(':');
                string action = cut >= 0 ? lower.Substring(cut + 1) : "";
                int score = 0;
                foreach (Regex finder in finders)
                {
                    if (finder.IsMatch(action))
                        score += 2;
                    else if (finder.IsMatch(lower))
                        score += 1;
                }
                if (score > bestScore)
                {
                    best = part;
                    bestScore = score;
                    tie = false;
                }
                else if (score == bestScore && score > 0)
                {
                    tie = true;
                }
            }
            return tie || bestScore == 0 ? null : best;
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

            // Removal that picks out cards one at a time: see "Removal aimed at single cards".
            bool aimKnown;
            Aim aim = AimOfActivation(out aimKnown);
            RemovalPlan plan = null;
            if (aim != null && !PlanRemoval(aim, aimKnown, p, out plan))
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
                && Duel.Phase == DuelPhase.Main1 && Card.IsAttack() && !Card.Attacked && AI.InOwnSection(Card)
                && CanAttackWell(Card))
                return false;   // attack first, use the effect in Main Phase 2

            _lastActivated = Card;
            _plan = plan;
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

        // What the effect being activated can hit, if it's "pick 1 card" removal (null if it
        // isn't). known = false when the card has several effects and we can't tell which one
        // this is; the result then covers all of them.
        private static readonly Dictionary<int, Aim> PartAims = new Dictionary<int, Aim>();
        private static readonly HashSet<int> UnmatchedEffects = new HashSet<int>();

        private Aim AimOfActivation(out bool known)
        {
            known = true;
            if (Card.Data == null || string.IsNullOrEmpty(Card.Data.Description))
                return null;
            string text = Card.Data.Description;
            int desc = ActivateDescription;
            int code = desc > 0 ? desc >> 4 : 0;
            bool named = code > 0 && (code == Card.Id || code == Card.Alias);
            bool several = HasSeveralEffects(text);

            // Several effects: the part of the text the effect's name points to.
            if (several && named)
            {
                Aim part = null;
                bool found;
                lock (Profiles)
                {
                    found = PartAims.TryGetValue(desc, out part);
                    if (!found && !UnmatchedEffects.Contains(desc))
                    {
                        string matched = MatchEffect(text, OptionText(desc));
                        if (matched != null)
                        {
                            part = ReadAim(matched);
                            PartAims[desc] = part;
                            found = true;
                        }
                        else
                        {
                            UnmatchedEffects.Add(desc);
                        }
                    }
                }
                if (found)
                    return part;
            }

            // The effect's name itself, when it says it all ("Destroy 1 card on the field").
            if (named)
            {
                Aim fromName;
                lock (Profiles)
                {
                    if (!EffectAims.TryGetValue(desc, out fromName))
                    {
                        fromName = ReadAim(OptionText(desc));
                        EffectAims[desc] = fromName;
                    }
                }
                if (fromName != null)
                    return fromName;
            }

            Aim whole;
            lock (Profiles)
            {
                if (!CardAims.TryGetValue(Card.Id, out whole))
                {
                    whole = ReadAim(text);
                    CardAims[Card.Id] = whole;
                }
            }
            // Activating a Spell/Trap card uses its main text; otherwise only trust the
            // whole text when the card has a single effect.
            bool activatingSpellTrap = (Is(Card, CardType.Spell) || Is(Card, CardType.Trap))
                && (Card.Location == CardLocation.Hand || Card.Location == CardLocation.SpellZone && Card.IsFacedown());
            if (several && !activatingSpellTrap)
                known = false;
            return whole;
        }

        // -----------------------------------------------------------------------------
        // Removal aimed at single cards ("Target 1 card on the field; destroy it").
        //
        // Many of these can hit either side of the field. The bot aims them at the opponent's
        // cards, and only at its own cards when:
        //  (2) the opponent's effect is aimed at our card, and removing it ourselves first is
        //      better: it saves the card (we return it to the hand, or banish it only until
        //      the End Phase), it stops them taking control of it, it spoils a follow-up that
        //      needs the card gone ("destroy it, and if you do, draw 1 card"; "both"), or our
        //      removal sets off the card's own effect and theirs wouldn't;
        //  (1) the opponent has nothing it can hit, and our card gains from being removed that
        //      way ("If this card is sent to the GY: ..."). Spells/Traps and other one-shot
        //      cards wait until the opponent's End Phase in case they set something; effects
        //      that can be used again don't wait, but an effect that stops the monster
        //      attacking waits for Main Phase 2.
        // Priority: (2), then the opponent's most threatening card, then (1).
        // The opponent's cards that gain from being removed a certain way ("If this card is
        // destroyed: ...") are picked after their other cards when we remove them that way.

        // How much less we want to hit an opponent's card whose own effect it would set off.
        private const int SetsOffEffectPenalty = 1000;

        private class RemovalPlan
        {
            public int SourceId;
            public RemovalKind Kind;
            public List<ClientCard> Own = new List<ClientCard>();  // our cards it may hit, best first
            public bool Response;   // rule (2): hit Own first
        }

        private RemovalPlan _plan;

        // Whether a removal effect is worth using now, and which of our own cards it may hit.
        private bool PlanRemoval(Aim aim, bool known, Profile p, out RemovalPlan plan)
        {
            plan = new RemovalPlan { SourceId = Card.Id, Kind = aim.Kind };
            int sourceType = SourceType();
            int theirs = 0;
            if (aim.Monsters)
                theirs += Enemy.GetMonsters().Count(c => CanAim(aim, c, sourceType));
            if (aim.Spells)
                theirs += Enemy.GetSpells().Count(c => CanAim(aim, c, sourceType));
            if (aim.Hand)
                theirs += Enemy.Hand.Count;
            if (aim.Grave)
                theirs += Enemy.Graveyard.Count;

            // Only the opponent's cards (or we can't tell which effect this is): only when
            // they have something it can hit.
            if (!known || !aim.EitherSide || aim.OwnRequired)
                return theirs > 0;

            // (2) Answering the opponent's effect aimed at one of our cards.
            if (Duel.CurrentChain.Count > 0 && Duel.LastChainPlayer == 1)
            {
                ClientCard ours = ResponseTarget(aim, sourceType);
                if (ours != null && theirs + 1 >= aim.MinTargets)
                {
                    plan.Own.Add(ours);
                    plan.Response = true;
                    return true;
                }
            }

            // The opponent's cards.
            if (theirs >= aim.MinTargets)
                return true;

            // (1) Our own cards that gain from being removed this way.
            if (!GoodTimeToHitOwnCard(aim, p))
                return false;
            bool missesTiming = Duel.CurrentChain.Count > 0 || aim.FollowUp;
            List<ClientCard> gainers = Bot.GetMonsters().Concat(Bot.GetSpells())
                .Where(c => c != Card && CanAim(aim, c, sourceType) && SetsOffOwnEffect(c, aim.Kind, true, missesTiming))
                .OrderBy(c => Value(c)).ToList();
            if (gainers.Count == 0 || theirs + gainers.Count < aim.MinTargets)
                return false;
            plan.Own = gainers;
            return true;
        }

        private bool GoodTimeToHitOwnCard(Aim aim, Profile p)
        {
            bool reusable = Card.IsFaceup() && !p.SelfCost
                && (Card.Location == CardLocation.MonsterZone || Card.Location == CardLocation.SpellZone);
            if (reusable)
            {
                if (aim.StopsAttack && Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 && Duel.Turn > 1
                    && Card.Location == CardLocation.MonsterZone && Card.IsAttack() && !Card.Attacked
                    && AI.InOwnSection(Card))
                    return false;   // attack first, use it in Main Phase 2
                return true;
            }
            // A one-shot card: keep it in case the opponent sets something better to hit.
            return Duel.Player == 1 && Duel.Phase == DuelPhase.End && Duel.CurrentChain.Count == 0;
        }

        // The kind of card the activated effect comes from, for "cannot be targeted by
        // Spell/Trap effects" and the like.
        private int SourceType()
        {
            if (Is(Card, CardType.Pendulum) && Card.Location == CardLocation.SpellZone)
                return (int)CardType.Spell;   // a Pendulum Effect
            if (Is(Card, CardType.Monster))
                return (int)CardType.Monster;
            if (Is(Card, CardType.Trap))
                return (int)CardType.Trap;
            return (int)CardType.Spell;
        }

        // Whether the effect can reach this card and actually do something to it.
        private bool CanAim(Aim aim, ClientCard card, int sourceType)
        {
            if (card == null)
                return false;
            bool zone = aim.Monsters && card.Location == CardLocation.MonsterZone
                || aim.Spells && card.Location == CardLocation.SpellZone;
            if (!zone)
                return false;
            if (aim.FaceUpOnly && !card.IsFaceup())
                return false;
            if (aim.FaceDownOnly && !card.IsFacedown())
                return false;
            return !Shielded(card, aim.Targets, aim.Kind, sourceType);
        }

        // Rule (2): the opponent's effect at the end of the chain is aimed at one of our
        // cards, and removing that card ourselves first is better. Returns that card, or null.
        private ClientCard ResponseTarget(Aim aim, int sourceType)
        {
            if (Duel.CurrentChainInfo.Count == 0)
                return null;
            ChainInfo last = Duel.CurrentChainInfo[Duel.CurrentChainInfo.Count - 1];
            if (last.ActivatePlayer != 1)
                return null;
            List<ClientCard> aimedAtUs = last.Targets
                .Where(c => c != null && c.Controller == 0 && c != Card && CanAim(aim, c, sourceType))
                .OrderByDescending(c => Value(c)).ToList();
            if (aimedAtUs.Count == 0)
                return null;
            string text = EffectTextOf(last);
            if (text == null)
                return null;
            TheirEffect theirs = ReadTheirEffect(text);
            bool removes = theirs.Kind != RemovalKind.Other;
            if (!removes && !theirs.TakesControl)
                return null;   // not taking the card away from us (e.g. negating it)

            foreach (ClientCard card in aimedAtUs)
            {
                // Saving it: back to our hand, or banished only until it comes back.
                bool losesIt = theirs.TakesControl || removes && theirs.Kind != RemovalKind.ToHand;
                if (losesIt && aim.Kind == RemovalKind.ToHand && !Is(card, CardType.Token) && !IsExtra(card))
                    return card;
                if (losesIt && aim.Kind == RemovalKind.Banish && aim.Temporary && !Is(card, CardType.Token))
                    return card;
                // Stopping them from taking it, or from getting their follow-up.
                if (theirs.TakesControl)
                    return card;
                if (theirs.NeedsEveryTarget || theirs.FollowUpNeedsRemoval && last.Targets.Count == 1)
                    return card;
                // Our way of removing it sets off its effect, and theirs wouldn't.
                if (SetsOffOwnEffect(card, aim.Kind, true, true) && !SetsOffOwnEffect(card, theirs.Kind, false, false))
                    return card;
            }
            return null;
        }

        // -----------------------------------------------------------------------------
        // Cards whose own effect goes off when they're removed ("If this card is sent to the
        // GY: ...", "If this card is destroyed: ...", "If this card leaves the field: ...").

        private class Trigger
        {
            public bool Destroyed, ToGrave, Banished, LeavesField, ToHand, ToDeck;
            public bool OpponentOnly;   // "by your opponent's card effect": not when we do it ourselves
            public bool NeedsGrave;     // "destroyed and sent to the GY" (destroyed Pendulum Monsters go to the Extra Deck)
            public bool OptionalWhen;   // "When ...: You can": misses the timing if something else happens after
        }

        private const string ThisCardPattern = @"(?<!while )\bthis ([\w-]+ ){0,3}card\b";
        private static readonly Regex DestroyedEventRx = new Regex(ThisCardPattern + @"[^:,]{0,60}?\b(is|was)\b(?<span>[^:]{0,40}?)\bdestroyed\b", Rx);
        private static readonly Regex GraveEventRx = new Regex(ThisCardPattern + @"[^:,]{0,60}?\b(is|was)\b(?<span>[^:]{0,40}?)\bsent\b[^:]{0,40}?\b(gy|graveyard)\b", Rx);
        private static readonly Regex BanishedEventRx = new Regex(ThisCardPattern + @"[^:,]{0,60}?\b(is|was)\b(?<span>[^:]{0,40}?)\bbanished\b", Rx);
        private static readonly Regex LeavesEventRx = new Regex(ThisCardPattern + @"(?<span>[^:,]{0,60}?)\bleaves the field\b", Rx);
        private static readonly Regex ToHandEventRx = new Regex(ThisCardPattern + @"[^:,]{0,60}?\b(is|was)\b(?<span>[^:]{0,40}?)\breturned to (the|your|its owner's) hand\b", Rx);
        private static readonly Regex ToDeckEventRx = new Regex(ThisCardPattern + @"[^:,]{0,60}?\b(is|was)\b(?<span>[^:]{0,40}?)\b(returned|shuffled) (in)?to the deck\b", Rx);
        private static readonly Regex IfWhenRx = new Regex(@"\b(if|when)\b", Rx);
        private static readonly Regex BattleWordRx = new Regex(@"\bbattle\b", Rx);
        private static readonly Regex EffectWordRx = new Regex(@"\beffects?\b", Rx);
        private static readonly Regex OpponentWordRx = new Regex(@"\bopponent", Rx);
        private static readonly Regex NotRemovalRx = new Regex(@"\bmaterials?\b|\btributed\b|\bdiscarded\b|\bas (a )?cost\b|\bfor (a|the) [\w\s-]*summon\b|\bexcavated\b|\bdetached\b|\bto activate\b|\bbecause\b|""", Rx);
        private static readonly Regex FromZoneRx = new Regex(@"\bfrom (the |your |its owner's |their )?(hand|deck|gy|graveyard|extra deck|main deck)\b", Rx);
        private static readonly Regex FieldWordRx = new Regex(@"\bfield\b", Rx);
        private static readonly Regex SpanBreakRx = new Regex(@"\band\b|,|\bor\b(?! card effect)", Rx);
        private static readonly Regex DestroyedAndSentRx = new Regex(@"\bdestroyed\b[^:]*\band sent to\b", Rx);
        private static readonly Regex BadPayoffRx = new Regex(@"\b(destroy|banish|tribute)\b[^.]*\b(equipped monster|that monster|that target|you control)\b|\byou (lose|take)\b|damage to you\b|\byour opponent (draws|gains|adds|special summons)\b", Rx);
        private static readonly Regex YouCanStartRx = new Regex(@"^\s*you can\b", Rx);
        private static readonly Regex LeadingNumberRx = new Regex(@"^\(\d+\)\s*", Rx);

        private static readonly Dictionary<int, List<Trigger>> TriggersById = new Dictionary<int, List<Trigger>>();

        private static List<Trigger> TriggersOf(int id)
        {
            lock (Profiles)
            {
                List<Trigger> list;
                if (!TriggersById.TryGetValue(id, out list))
                {
                    NamedCard data = NamedCard.Get(id);
                    list = data == null ? new List<Trigger>() : ReadTriggers(data.Description);
                    TriggersById[id] = list;
                }
                return list;
            }
        }

        private static List<Trigger> ReadTriggers(string description)
        {
            List<Trigger> list = new List<Trigger>();
            if (string.IsNullOrEmpty(description))
                return list;
            string t = description.Replace("\r\n", "\n").Replace('\r', '\n');
            for (int i = t.IndexOf(':'); i >= 0; i = t.IndexOf(':', i + 1))
            {
                // The condition before the ":" and what happens after it.
                int start = Math.Max(Math.Max(t.LastIndexOf('.', i), t.LastIndexOf('\u25CF', i)),
                                     Math.Max(t.LastIndexOf(']', i), t.LastIndexOf('\n', i))) + 1;
                string condition = LeadingNumberRx.Replace(t.Substring(start, i - start).Trim(), "");
                int end = t.Length;
                foreach (char boundary in new[] { '.', '\u25CF', '\n' })
                {
                    int j = t.IndexOf(boundary, i + 1);
                    if (j >= 0 && j < end)
                        end = j;
                }
                string payoff = t.Substring(i + 1, end - i - 1);
                if (!IfWhenRx.IsMatch(condition))
                    continue;

                Trigger trigger = new Trigger
                {
                    Destroyed = HasEvent(DestroyedEventRx, condition),
                    ToGrave = HasEvent(GraveEventRx, condition),
                    Banished = HasEvent(BanishedEventRx, condition),
                    LeavesField = HasEvent(LeavesEventRx, condition),
                    ToHand = HasEvent(ToHandEventRx, condition),
                    ToDeck = HasEvent(ToDeckEventRx, condition),
                };
                if (!(trigger.Destroyed || trigger.ToGrave || trigger.Banished || trigger.LeavesField || trigger.ToHand || trigger.ToDeck))
                    continue;
                if (NotRemovalRx.IsMatch(condition))
                    continue;   // used as material, Tributed, discarded, as a cost...
                if (BattleWordRx.IsMatch(condition) && !EffectWordRx.IsMatch(condition))
                    continue;   // only when destroyed by battle
                if (FromZoneRx.IsMatch(condition) && !FieldWordRx.IsMatch(condition))
                    continue;   // only from the hand, Deck or GY
                if (BadPayoffRx.IsMatch(payoff))
                    continue;   // e.g. "destroy the equipped monster": hurts its owner
                trigger.OpponentOnly = OpponentWordRx.IsMatch(condition);
                trigger.NeedsGrave = trigger.Destroyed && DestroyedAndSentRx.IsMatch(condition);
                trigger.OptionalWhen = condition.TrimStart('(', ' ').StartsWith("when", StringComparison.OrdinalIgnoreCase)
                    && YouCanStartRx.IsMatch(payoff);
                list.Add(trigger);
            }
            return list;
        }

        private static bool HasEvent(Regex rx, string condition)
        {
            Match m = rx.Match(condition);
            return m.Success && !SpanBreakRx.IsMatch(m.Groups["span"].Value);
        }

        // True if removing this card that way sets off one of its own effects. ours: we're the
        // ones removing our own card (effects that need the opponent to do it don't count).
        // missesTiming: something else happens after the removal, so "When ...: You can"
        // effects miss their timing.
        private static bool SetsOffOwnEffect(ClientCard card, RemovalKind kind, bool ours, bool missesTiming)
        {
            if (card == null || kind == RemovalKind.Other || !Known(card) || Is(card, CardType.Token))
                return false;
            bool pendulum = Is(card, CardType.Pendulum);
            foreach (Trigger t in TriggersOf(card.Id))
            {
                if (ours && t.OpponentOnly)
                    continue;
                if (missesTiming && t.OptionalWhen)
                    continue;
                switch (kind)
                {
                    case RemovalKind.Destroy:
                        if (t.LeavesField || t.Destroyed && !(pendulum && t.NeedsGrave) || t.ToGrave && !pendulum)
                            return true;
                        break;
                    case RemovalKind.Banish:
                        if (t.Banished || t.LeavesField)
                            return true;
                        break;
                    case RemovalKind.ToHand:
                        if (t.ToHand || t.LeavesField)
                            return true;
                        break;
                    case RemovalKind.ToDeck:
                        if (t.ToDeck || t.LeavesField)
                            return true;
                        break;
                    case RemovalKind.ToGrave:
                        if (t.ToGrave || t.LeavesField)
                            return true;
                        break;
                }
            }
            return false;
        }

        // -----------------------------------------------------------------------------
        // Protection read from card text: "cannot be targeted by card effects", "unaffected by
        // other cards' effects", "cannot be destroyed by card effects", for the card itself or
        // for others ("monsters you control...", "the equipped monster..."). Conditions
        // ("while it has material") are ignored, so this errs on the side of "protected".

        private enum ProtectionKind { Untargetable, Unaffected, Indestructible }
        private enum ProtectionScope { Self, Equipped, Monsters, SpellTraps, Cards }

        private class Protection
        {
            public ProtectionKind Kind;
            public int From;             // CardType bits of the effects it stops (Monster, Spell, Trap)
            public bool OpponentOnly;    // only against the opponent's effects
            public ProtectionScope Scope;
            public string Archetype;     // "\"X\" monsters you control": only cards with X in their name
            public bool OthersOnly;      // "other monsters you control"
        }

        private static readonly Regex UntargetableRx = new Regex(@"(?<subj>[^.:;\u25CF\n]*?)\bcannot be targeted\b(?<by>[^.;\u25CF\n]*)", Rx);
        private static readonly Regex NoTargetingRx = new Regex(@"(?<subj>[^.:;\u25CF\n]*?)\b(?<who>your opponent|neither player|players) (cannot|can) target (?<obj>[^.;\u25CF\n]*?) with (?<by>[^.;\u25CF\n]*?effects?)", Rx);
        private static readonly Regex UnaffectedRx = new Regex(@"(?<subj>[^.:;\u25CF\n]*?)\bunaffected by (?<by>[^.;\u25CF\n]*?effects?)", Rx);
        private static readonly Regex IndestructibleRx = new Regex(@"(?<subj>[^.:;\u25CF\n]*?)\bcannot be (targeted or )?destroyed by (?<by>[^.;\u25CF\n]*?effects?)", Rx);
        private static readonly Regex SubjectCutRx = new Regex(@"\b(cannot|can't|are|is|gains?)\b", Rx);
        private static readonly Regex LeadingAndRx = new Regex(@"^(and|also)\b", Rx);
        private static readonly Regex OtherWordRx = new Regex(@"\bother\b", Rx);
        private static readonly Regex QuotedRx = new Regex("\"([^\"]+)\"");
        private const int AnyCardType = (int)CardType.Monster | (int)CardType.Spell | (int)CardType.Trap;

        private static readonly Dictionary<int, List<Protection>> ProtectionsById = new Dictionary<int, List<Protection>>();

        private static List<Protection> ProtectionsOf(int id)
        {
            lock (Profiles)
            {
                List<Protection> list;
                if (!ProtectionsById.TryGetValue(id, out list))
                {
                    NamedCard data = NamedCard.Get(id);
                    list = data == null ? new List<Protection>() : ReadProtection(data.Description);
                    ProtectionsById[id] = list;
                }
                return list;
            }
        }

        private static List<Protection> ReadProtection(string description)
        {
            List<Protection> list = new List<Protection>();
            if (string.IsNullOrEmpty(description))
                return list;
            string t = description.Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (Match m in UntargetableRx.Matches(t))
                AddProtection(list, ProtectionKind.Untargetable, m.Groups["subj"].Value, m.Groups["by"].Value, null);
            foreach (Match m in UnaffectedRx.Matches(t))
                AddProtection(list, ProtectionKind.Unaffected, m.Groups["subj"].Value, m.Groups["by"].Value, null);
            foreach (Match m in IndestructibleRx.Matches(t))
                AddProtection(list, ProtectionKind.Indestructible, m.Groups["subj"].Value, m.Groups["by"].Value, null);
            foreach (Match m in NoTargetingRx.Matches(t))
            {
                // "Your opponent cannot target this card / them / monsters you control with card effects."
                string obj = m.Groups["obj"].Value.Trim().ToLowerInvariant();
                string subject = obj == "this card" || obj == "it" ? ""
                    : obj == "them" ? SubjectCutRx.Split(m.Groups["subj"].Value)[0]
                    : obj;
                bool opponentOnly = m.Groups["who"].Value.Equals("your opponent", StringComparison.OrdinalIgnoreCase);
                AddProtection(list, ProtectionKind.Untargetable, subject, m.Groups["by"].Value, opponentOnly);
            }
            return list;
        }

        private static void AddProtection(List<Protection> list, ProtectionKind kind, string subject, string by, bool? opponentOnly)
        {
            string b = by.ToLowerInvariant();
            if (!b.Contains("effect"))
                return;   // e.g. "cannot be targeted for attacks"
            Protection rule = new Protection { Kind = kind, OpponentOnly = opponentOnly ?? b.Contains("opponent") };
            if (b.Contains("spell/trap") || b.Contains("spell") && b.Contains("trap"))
                rule.From = (int)CardType.Spell | (int)CardType.Trap;
            else if (b.Contains("spell"))
                rule.From = (int)CardType.Spell;
            else if (b.Contains("trap"))
                rule.From = (int)CardType.Trap;
            else if (b.Contains("monster"))
                rule.From = (int)CardType.Monster;
            else
                rule.From = AnyCardType;

            // Whose protection: the text before it, after any "While ...," condition.
            string s = subject.ToLowerInvariant();
            int comma = s.LastIndexOf(',');
            if (comma >= 0)
                s = s.Substring(comma + 1);
            s = LeadingAndRx.Replace(s.Trim(), "").Trim();
            if (s.Length == 0 || s.StartsWith("this ") || s == "it")
            {
                rule.Scope = ProtectionScope.Self;
            }
            else if (s.Contains("equipped monster"))
            {
                rule.Scope = ProtectionScope.Equipped;
            }
            else if (s.Contains("you control"))
            {
                Match quoted = QuotedRx.Match(s);
                if (quoted.Success)
                    rule.Archetype = quoted.Groups[1].Value;
                rule.Scope = s.Contains("monster") ? ProtectionScope.Monsters
                    : s.Contains("spell") || s.Contains("trap") ? ProtectionScope.SpellTraps
                    : ProtectionScope.Cards;
                rule.OthersOnly = OtherWordRx.IsMatch(s);
            }
            else
            {
                return;   // something we can't place ("Linked monsters", ...)
            }
            list.Add(rule);
        }

        // True if, as far as we can tell, this card can't be hit by the effect we're using:
        // its own protection, or protection from a card on its controller's side.
        private bool Shielded(ClientCard card, bool targets, RemovalKind kind, int sourceType)
        {
            if (targets && card.Controller == 1 && card.IsShouldNotBeTarget())
                return true;
            bool fromItsOpponent = card.Controller == 1;
            ClientField side = card.Controller == 0 ? Bot : Enemy;
            foreach (ClientCard giver in side.GetMonsters().Concat(side.GetSpells()))
            {
                if (giver == null || !giver.IsFaceup() || giver.IsDisabled() || !Known(giver))
                    continue;
                foreach (Protection rule in ProtectionsOf(giver.Id))
                {
                    if (rule.OpponentOnly && !fromItsOpponent)
                        continue;
                    if ((rule.From & sourceType) == 0)
                        continue;
                    if (!Covers(rule, giver, card))
                        continue;
                    if (rule.Kind == ProtectionKind.Unaffected)
                        return true;
                    if (rule.Kind == ProtectionKind.Untargetable && targets)
                        return true;
                    if (rule.Kind == ProtectionKind.Indestructible && kind == RemovalKind.Destroy)
                        return true;
                }
            }
            return false;
        }

        private static bool Covers(Protection rule, ClientCard giver, ClientCard card)
        {
            switch (rule.Scope)
            {
                case ProtectionScope.Self:
                    return giver == card;
                case ProtectionScope.Equipped:
                    return giver.EquipTarget == card;
                default:
                    if (rule.OthersOnly && giver == card)
                        return false;
                    if (rule.Scope == ProtectionScope.Monsters && card.Location != CardLocation.MonsterZone)
                        return false;
                    if (rule.Scope == ProtectionScope.SpellTraps && card.Location != CardLocation.SpellZone)
                        return false;
                    if (rule.Archetype != null && (card.Name == null
                        || card.Name.IndexOf(rule.Archetype, StringComparison.OrdinalIgnoreCase) < 0))
                        return false;
                    return true;
            }
        }

        // -----------------------------------------------------------------------------
        // Reading the opponent's effect at the end of the chain, for rule (2).

        private class TheirEffect
        {
            public RemovalKind Kind;           // how it removes its target (Other: it doesn't)
            public bool TakesControl;
            public bool NeedsEveryTarget;      // "both", or switching control: fails if one target is gone
            public bool FollowUpNeedsRemoval;  // "destroy it, and if you do / then ..."
        }

        private static readonly Regex BothRx = new Regex(@"\bboth\b|\b(switch|swap|exchange) control\b", Rx);
        private static readonly Regex ControlRx = new Regex(@"\b(take|gain|switch|swap|exchange) control\b", Rx);
        private static readonly Regex FollowSplitRx = new Regex(@"\b(?:and if you do|then|after that)\b", Rx);
        private static readonly Regex RemoveVerbRx = new Regex(@"\b(destroy|banish|return|shuffle|send|take control|gain control)\b", Rx);

        private static TheirEffect ReadTheirEffect(string effectText)
        {
            string t = effectText.Replace('\r', ' ').Replace('\n', ' ');
            // What it does to its target: the part after the ";" of the sentence that targets.
            string action = null;
            foreach (string sentence in t.Split('.'))
            {
                int semicolon = sentence.IndexOf(';');
                if (semicolon >= 0 && sentence.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    action = sentence.Substring(semicolon + 1);
                    break;
                }
            }
            if (action == null)
            {
                int semicolon = t.IndexOf(';');
                action = semicolon >= 0 ? t.Substring(semicolon + 1) : t;
            }
            string[] parts = FollowSplitRx.Split(action, 2);
            string first = parts[0];
            bool firstRemoves = RemoveVerbRx.IsMatch(first) && TargetRefRx.IsMatch(first);
            bool followUp = parts.Length > 1 && parts[1].Trim(' ', ',', '.').Length > 3;
            return new TheirEffect
            {
                Kind = KindOf(action),
                TakesControl = ControlRx.IsMatch(t),
                NeedsEveryTarget = BothRx.IsMatch(action),
                FollowUpNeedsRemoval = firstRemoves && followUp,
            };
        }

        // The text of the effect a chain link is using (the matching part, for cards with several).
        private static string EffectTextOf(ChainInfo info)
        {
            NamedCard data = NamedCard.Get(info.ActivateId);
            if (data == null || string.IsNullOrEmpty(data.Description))
                return null;
            string text = data.Description;
            if (!HasSeveralEffects(text))
                return text;
            int desc = info.ActivateDescription;
            int code = desc > 0 ? desc >> 4 : 0;
            if (code > 0 && (code == info.ActivateId || code == info.ActivateAlias))
            {
                string part = MatchEffect(text, OptionText(desc));
                if (part != null)
                    return part;
            }
            return text;
        }

        private static RemovalKind KindOfHint(int hint)
        {
            switch (hint)
            {
                case HintMsg.Destroy:
                    return RemovalKind.Destroy;
                case HintMsg.Remove:
                    return RemovalKind.Banish;
                case HintMsg.ReturnToHand:
                    return RemovalKind.ToHand;
                case HintMsg.ToDeck:
                    return RemovalKind.ToDeck;
                case HintMsg.ToGrave:
                    return RemovalKind.ToGrave;
                default:
                    return RemovalKind.Other;
            }
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
            _plan = null;
        }

        public override void OnNewPhase()
        {
            base.OnNewPhase();
            _lastActivated = null;
            _plan = null;
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
            // Only Tribute monsters worth less than what we'd get (by the same Value score
            // OnSelectTribute uses, counting the new monster at its best stat, on the field).
            int newValue = Math.Max(Atk(card), Def(card)) + level * 30 + 300;
            if (Is(card, CardType.Effect))
                newValue += 250;
            int cheaper = Bot.GetMonsters().Count(m => m != card && Value(m) < newValue);
            return cheaper >= tributes;
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

        // What we assume an unidentified face-down monster's DEF to be: the bot only attacks
        // one with a monster whose ATK is higher than this (see OnPreBattleBetween).
        private const int UnknownFaceDownDefense = 1300;

        // True = Set it face-down instead of Normal Summoning it.
        public override bool OnSelectMonsterSummonOrSet(ClientCard card)
        {
            if (Is(card, CardType.Flip))
                return true;   // FLIP effects need it face-down
            if (HasSummonEffect(card))
                return false;  // Setting it would waste its "when Summoned" effect
            return !SummonToAttack(Atk(card), Def(card), null, card.Id);
        }

        // "If this card is Normal Summoned: ..." and the like.
        private static readonly Regex SummonEffectRx = new Regex(@"\b(if|when) this card is (normal |tribute |normal or special |normal or flip )?summoned\b", Rx);

        private static bool HasSummonEffect(ClientCard card)
        {
            return card.Data != null && !string.IsNullOrEmpty(card.Data.Description)
                && SummonEffectRx.IsMatch(card.Data.Description);
        }

        // Whether we still get to attack with this monster (or one we're about to summon:
        // null) this turn: our Main Phase 1 with a Battle Phase ahead, after the first turn,
        // and (in split-zone tag duels) in our own section of the field.
        private bool CanAttackThisTurn(ClientCard self)
        {
            return Duel.Player == 0 && Duel.Phase == DuelPhase.Main1 && Duel.Turn > 1
                && (Duel.MainPhase == null || Duel.MainPhase.CanBattlePhase)
                && (self == null || AI.InOwnSection(self));
        }

        // Attack Position (true) or defending (false), for a monster being summoned, Set or
        // repositioned ("self" is the monster itself when it's already on the field; newCardId
        // the card about to be summoned otherwise):
        //  - in Main Phase 1, with a Battle Phase ahead: attack if the monster has a part in
        //    this turn's battle plan (see "Battle plan" below)
        //  - otherwise, attack if its ATK is higher than its DEF and higher than the ATK of
        //    every opponent's monster we know of that will still be there after our attacks
        //    (all of them, when we don't get to attack this turn)
        private bool SummonToAttack(int atk, int def, ClientCard self = null, int newCardId = 0)
        {
            BattlePlan plan = null;
            if (CanAttackThisTurn(self))
            {
                Fighter extra = self == null ? NewFighter(null, atk, newCardId) : null;
                plan = MainPhasePlan(extra);
                if (plan.Attacks.Keys.Any(f => self != null ? f.Card == self : f == extra))
                    return true;
            }
            return atk > 0 && atk > def && atk > StrongestSurvivorAtk(plan);
        }

        // The highest ATK among the opponent's monsters we know of (face-up, or face-down but
        // identified), leaving out the ones our battle plan destroys. -1 if there are none.
        private int StrongestSurvivorAtk(BattlePlan plan)
        {
            return Enemy.GetMonsters()
                .Where(m => (m.IsFaceup() || m.Id != 0) && (plan == null || !plan.Destroyed.Contains(m)))
                .Select(m => m.IsFaceup() ? m.Attack : (m.Data != null ? m.Data.Attack : 0))
                .DefaultIfEmpty(-1).Max();
        }

        // What an attacker's ATK has to beat to destroy a monster in battle: its ATK in
        // Attack Position, its DEF in Defense Position, or our guess for an unidentified
        // face-down monster.
        private static int Toughness(ClientCard monster)
        {
            if (monster.IsFacedown())
                return monster.Id == 0 ? UnknownFaceDownDefense : monster.Defense;
            return monster.GetDefensePower();
        }

        // Changing a monster's battle position uses the same test, monster by monster.
        private bool SmartRepos()
        {
            // A face-down FLIP monster stays face-down unless its FLIP effect has a use (and,
            // for removal, one of the opponent's cards to hit).
            if (Card.IsFacedown() && Is(Card, CardType.Flip))
                return FlipEffectUsable(Card);
            if (Card.IsMonsterInvincible())
                return Card.IsDefense();
            if ((TraitsOf(Card.Id) & BattleTraits.AttacksInDefense) != 0)
                return Card.IsAttack();   // it can attack from Defense Position, so it stays there
            bool attack = SummonToAttack(Atk(Card), Def(Card), Card);
            return Card.IsAttack() ? !attack : attack;
        }

        // A face-down FLIP monster's FLIP effect: whether it would do something useful now.
        private static readonly Regex FlipEffectRx = new Regex(@"\bFLIP:\s*(?<effect>[^\n\u25CF]*)", RegexOptions.CultureInvariant);

        private bool FlipEffectUsable(ClientCard card)
        {
            if (card.Data == null || string.IsNullOrEmpty(card.Data.Description))
                return false;
            Match m = FlipEffectRx.Match(card.Data.Description.Replace("\r\n", "\n"));
            if (!m.Success)
                return false;
            string effect = m.Groups["effect"].Value;
            Aim aim = ReadAim(effect);
            if (aim != null)
            {
                // Removal: only when the opponent has something it can hit, never just our own cards.
                int sourceType = (int)CardType.Monster;
                int theirs = 0;
                if (aim.Monsters)
                    theirs += Enemy.GetMonsters().Count(c => CanAim(aim, c, sourceType));
                if (aim.Spells)
                    theirs += Enemy.GetSpells().Count(c => CanAim(aim, c, sourceType));
                if (aim.Hand)
                    theirs += Enemy.Hand.Count;
                if (aim.Grave)
                    theirs += Enemy.Graveyard.Count;
                return theirs >= aim.MinTargets;
            }
            Profile p = ReadProfile(effect);
            if (p.MassOpponent)
                return Enemy.GetFieldCount() > 0;
            if (p.MassMonsters || p.MassSpells)
                return WipeHelpsUs(p);
            if (!p.Helpful)
                return false;
            // "Target 1 monster in your GY; Special Summon it": there has to be one.
            Match gy = GyTargetRx.Match(effect);
            if (gy.Success)
            {
                string whose = gy.Groups["whose"].Value.ToLowerInvariant();
                bool monstersOnly = gy.Value.IndexOf("monster", StringComparison.OrdinalIgnoreCase) >= 0;
                IEnumerable<ClientCard> pool = whose == "your" ? Bot.Graveyard
                    : whose == "either" ? Bot.Graveyard.Concat(Enemy.Graveyard)
                    : Enemy.Graveyard;
                return pool.Any(c => c != null && (!monstersOnly || Is(c, CardType.Monster)));
            }
            return true;
        }

        private static readonly Regex GyTargetRx = new Regex(@"\btarget\b[^.;]*?\bin (?<whose>your opponent's|your|either) (gy|graveyard)\b", Rx);

        // -----------------------------------------------------------------------------
        // Battle plan: which of our monsters attack what this turn.
        //
        // Every way of pairing our attackers with opposing monsters they can beat is tried
        // (each opposing monster attacked once; when they're all gone, the rest of our
        // attackers go direct), and the best one is kept:
        //  - a combination that brings the opponent's LP to 0 beats everything;
        //  - otherwise: the threat score of what we destroy, plus the damage we deal, minus
        //    the value of our monsters lost in equal-ATK trades;
        //  - between equally good plans, the one using weaker attackers, keeping the strong
        //    ones free (e.g. the 2000 clears a Defense Position 1900 so the 3000 goes direct).
        // Attacks that would do nothing (no damage, nothing destroyed) aren't part of a plan,
        // unless the monster has an effect when it attacks.
        // In the Battle Phase the bot follows the plan and re-plans after every attack (a
        // face-down monster may turn out tougher than our guess). In Main Phase 1 the plan
        // decides positions: monsters with a part in it go to Attack Position; the rest only
        // if they're strong enough to stand there (see SummonToAttack).

        [Flags]
        private enum BattleTraits
        {
            None = 0,
            Piercing = 1,          // "inflict piercing battle damage"
            Direct = 2,            // "can attack directly"
            AttackEffect = 4,      // "When this card declares an attack" / "If this card attacks"
            AttacksInDefense = 8,  // "can attack while in face-up Defense Position"
            DefenseIsPower = 16,   // ...and uses its DEF for damage calculation
        }

        private static readonly Regex PiercingRx = new Regex(@"\bpiercing (battle )?damage\b", Rx);
        private static readonly Regex DirectAttackRx = new Regex(@"\bthis card can attack (your opponent )?directly\b", Rx);
        private static readonly Regex AttackEffectRx = new Regex(@"\b(when|if) this card (declares an attack|attacks)\b", Rx);
        private static readonly Regex AttacksInDefenseRx = new Regex(@"\bcan attack while (it is )?in (face-up )?defense position\b", Rx);
        private static readonly Regex DefenseIsPowerRx = new Regex(@"\b(its|this card's) def for (its )?damage calculation\b|\b(use|apply) its def\b", Rx);
        private static readonly Dictionary<int, BattleTraits> TraitsById = new Dictionary<int, BattleTraits>();

        private static BattleTraits TraitsOf(int id)
        {
            lock (Profiles)
            {
                BattleTraits traits;
                if (TraitsById.TryGetValue(id, out traits))
                    return traits;
                traits = BattleTraits.None;
                NamedCard data = NamedCard.Get(id);
                string text = data != null ? data.Description : null;
                if (!string.IsNullOrEmpty(text))
                {
                    if (PiercingRx.IsMatch(text))
                        traits |= BattleTraits.Piercing;
                    if (DirectAttackRx.IsMatch(text))
                        traits |= BattleTraits.Direct;
                    if (AttackEffectRx.IsMatch(text))
                        traits |= BattleTraits.AttackEffect;
                    if (AttacksInDefenseRx.IsMatch(text))
                        traits |= BattleTraits.AttacksInDefense;
                    if (DefenseIsPowerRx.IsMatch(text))
                        traits |= BattleTraits.DefenseIsPower;
                }
                TraitsById[id] = traits;
                return traits;
            }
        }

        private class Fighter
        {
            public ClientCard Card;      // null: a monster we're about to summon
            public int CardId;
            public int Power;
            public bool Direct;          // can attack directly even with monsters in the way
            public bool Piercing;
            public bool AttackEffect;    // worth attacking with even when it deals no damage
            public int Value;            // what losing it costs us
        }

        private class Defender
        {
            public ClientCard Card;
            public bool AttackPosition;
            public int Threat;
        }

        // What one attack would do.
        private struct Outcome
        {
            public bool Possible;    // worth declaring at all
            public bool Destroys;    // the defender is destroyed
            public bool Trade;       // ...and so is our attacker (equal ATK)
            public int Damage;       // to the opponent
        }

        private class BattlePlan
        {
            public Dictionary<Fighter, Defender> Attacks = new Dictionary<Fighter, Defender>();  // null: direct attack
            public HashSet<ClientCard> Destroyed = new HashSet<ClientCard>();
            public long Score;
        }

        private Fighter NewFighter(ClientCard card, int power, int cardId)
        {
            BattleTraits traits = TraitsOf(cardId);
            return new Fighter
            {
                Card = card,
                CardId = cardId,
                Power = Math.Max(0, power),
                Direct = (traits & BattleTraits.Direct) != 0,
                Piercing = (traits & BattleTraits.Piercing) != 0,
                AttackEffect = (traits & BattleTraits.AttackEffect) != 0,
                Value = card != null ? Value(card) : Math.Max(0, power) + 300,
            };
        }

        // Main Phase 1: the plan with every monster that could attack this turn: those in
        // Attack Position, those we could still switch (or Flip Summon) to Attack Position,
        // those that attack from Defense Position, plus "extra", a monster about to be summoned.
        private BattlePlan MainPhasePlan(Fighter extra)
        {
            List<Fighter> fighters = new List<Fighter>();
            IList<ClientCard> switchable = Duel.MainPhase != null ? Duel.MainPhase.ReposableCards : null;
            foreach (ClientCard m in Bot.GetMonsters())
            {
                if (m.Attacked || !AI.InOwnSection(m))
                    continue;
                BattleTraits traits = TraitsOf(m.Id);
                bool inAttack = m.IsFaceup() && m.IsAttack();
                bool fromDefense = m.IsFaceup() && m.IsDefense() && (traits & BattleTraits.AttacksInDefense) != 0;
                bool canSwitch = switchable != null && switchable.Contains(m);
                if (canSwitch && m.IsFacedown() && Is(m, CardType.Flip) && !FlipEffectUsable(m))
                    canSwitch = false;   // stays face-down (see SmartRepos)
                if (!inAttack && !fromDefense && !canSwitch)
                    continue;
                int power = fromDefense && (traits & BattleTraits.DefenseIsPower) != 0 ? Def(m) : Atk(m);
                fighters.Add(NewFighter(m, power, m.Id));
            }
            if (extra != null)
                fighters.Add(extra);
            return PlanBattle(fighters, Enemy.GetMonsters(), false);
        }

        // Battle Phase: follow the plan, one attack at a time.
        public override BattlePhaseAction OnBattle(IList<ClientCard> attackers, IList<ClientCard> defenders)
        {
            List<Fighter> fighters = attackers.Select(a => NewFighter(a, a.GetAttackPower(), a.Id)).ToList();
            foreach (Fighter f in fighters)
                f.Direct = f.Card.CanDirectAttack;
            BattlePlan plan = PlanBattle(fighters, defenders, true);
            if (plan.Attacks.Count == 0)
            {
                if (Duel.BattlePhase.CanMainPhaseTwo)
                    return AI.ToMainPhase2();
                if (Duel.BattlePhase.CanEndPhase)
                    return AI.ToEndPhase();
                return null;
            }
            // Monsters we can identify first (biggest threats first), then unidentified
            // face-down ones, then direct attacks, strongest first.
            KeyValuePair<Fighter, Defender> next = plan.Attacks
                .OrderBy(a => a.Value == null ? 2 : (a.Value.Card.IsFacedown() && a.Value.Card.Id == 0 ? 1 : 0))
                .ThenByDescending(a => a.Value == null ? a.Key.Power : a.Value.Threat)
                .First();
            return AI.Attack(next.Key.Card, next.Value != null ? next.Value.Card : null);
        }

        private Outcome Fight(Fighter f, ClientCard defender, bool inBattle)
        {
            Outcome o = new Outcome();
            int power, toughness;
            if (f.Card != null)
            {
                // Lets the usual checks have their say (dangerous or unbeatable monsters, our
                // guess for face-down ones...).
                f.Card.RealPower = inBattle ? f.Card.GetAttackPower() : f.Power;
                defender.RealPower = defender.GetDefensePower();
                if (!OnPreBattleBetween(f.Card, defender))
                    return o;
                power = f.Card.RealPower;
                toughness = defender.RealPower;
            }
            else
            {
                if (defender.IsMonsterDangerous() || defender.IsMonsterInvincible() && defender.IsDefense())
                    return o;
                power = f.Power;
                toughness = Toughness(defender);
            }
            bool attackPosition = defender.IsFaceup() && defender.IsAttack();
            bool indestructible = defender.IsMonsterInvincible();
            if (power > toughness)
            {
                o.Destroys = !indestructible;
                o.Damage = (attackPosition || f.Piercing) ? power - toughness : 0;
            }
            else if (power == toughness && attackPosition && power > 0 && !indestructible)
            {
                o.Destroys = true;
                o.Trade = true;
            }
            o.Possible = o.Destroys || o.Damage > 0;
            return o;
        }

        private BattlePlan PlanBattle(List<Fighter> fighters, IList<ClientCard> enemies, bool inBattle)
        {
            List<Defender> defenders = enemies.Where(c => c != null).Select(c => new Defender
            {
                Card = c,
                AttackPosition = c.IsFaceup() && c.IsAttack(),
                Threat = Threat(c),
            }).ToList();
            Outcome[,] outcomes = new Outcome[fighters.Count, defenders.Count];
            for (int i = 0; i < fighters.Count; ++i)
                for (int j = 0; j < defenders.Count; ++j)
                    outcomes[i, j] = Fight(fighters[i], defenders[j].Card, inBattle);

            BattlePlan best = new BattlePlan { Score = 0 };   // attacking with nothing scores 0
            int[] assigned = new int[defenders.Count];         // fighter attacking each defender, or -1
            bool[] used = new bool[fighters.Count];
            for (int j = 0; j < defenders.Count; ++j)
                assigned[j] = -1;
            int lifePoints = Enemy.LifePoints;

            Action<int> search = null;
            search = j =>
            {
                if (j < defenders.Count)
                {
                    search(j + 1);   // leave this one alone
                    for (int i = 0; i < fighters.Count; ++i)
                    {
                        if (used[i] || !outcomes[i, j].Possible)
                            continue;
                        if (outcomes[i, j].Trade && defenders[j].Threat <= fighters[i].Value)
                            continue;   // an even trade has to be worth it
                        used[i] = true;
                        assigned[j] = i;
                        search(j + 1);
                        assigned[j] = -1;
                        used[i] = false;
                    }
                    return;
                }
                // Every defender decided: score this plan.
                BattlePlan plan = new BattlePlan();
                long score = 0, damage = 0, strength = 0;
                bool allGone = true;
                for (int d = 0; d < defenders.Count; ++d)
                {
                    int i = assigned[d];
                    if (i < 0)
                    {
                        allGone = false;
                        continue;
                    }
                    Outcome o = outcomes[i, d];
                    plan.Attacks[fighters[i]] = defenders[d];
                    damage += o.Damage;
                    strength += fighters[i].Power;
                    if (o.Destroys)
                    {
                        score += defenders[d].Threat;
                        plan.Destroyed.Add(defenders[d].Card);
                    }
                    else
                    {
                        allGone = false;
                    }
                    if (o.Trade)
                        score -= fighters[i].Value;
                    if (fighters[i].AttackEffect)
                        score += 200;
                }
                for (int i = 0; i < fighters.Count; ++i)
                {
                    if (used[i] || !(allGone || fighters[i].Direct))
                        continue;
                    if (fighters[i].Power <= 0 && !fighters[i].AttackEffect)
                        continue;   // a direct attack that does nothing
                    plan.Attacks[fighters[i]] = null;
                    damage += fighters[i].Power;
                    strength += fighters[i].Power;
                    if (fighters[i].AttackEffect)
                        score += 200;
                }
                if (plan.Attacks.Count == 0)
                    return;
                score += damage;
                if (lifePoints > 0 && damage >= lifePoints)
                    score += 1000000000L;   // that's the duel
                score = score * 1000 - strength;   // equal plans: the one using weaker attackers
                if (score > 0 && (best.Attacks.Count == 0 || score > best.Score))
                {
                    plan.Score = score;
                    best = plan;
                }
            };
            search(0);
            return best;
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
                wanted = SummonToAttack(atk, Math.Max(0, def), null, cardId) ? CardPosition.FaceUpAttack : CardPosition.FaceUpDefence;
            return positions.Contains(wanted) ? wanted : (CardPosition)0;
        }

        // -----------------------------------------------------------------------------
        // Battle: an unknown face-down monster could have a solid DEF, so don't throw
        // weak monsters at it.

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (!base.OnPreBattleBetween(attacker, defender))
                return false;
            if (defender.IsFacedown() && defender.Id == 0 && defender.RealPower < UnknownFaceDownDefense)
                defender.RealPower = UnknownFaceDownDefense;
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

            // The plan made when we activated this removal effect (see PlanRemoval).
            RemovalPlan plan = _plan != null && SourceCardId() == _plan.SourceId ? _plan : null;

            Intent intent = IntentOfHint(hint);
            if (intent == Intent.Unknown && plan != null)
                intent = Intent.Harm;   // e.g. a plain "select the target" for our removal
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
                RemovalKind kind = KindOfHint(hint);
                if (kind == RemovalKind.Other && plan != null)
                    kind = plan.Kind;
                bool planUsed = plan != null && cards.Any(c => plan.Own.Contains(c));
                // Rule (2): our card that the opponent's effect is aimed at comes first.
                if (plan != null && plan.Response)
                {
                    foreach (ClientCard card in plan.Own)
                    {
                        if (cards.Contains(card) && picked.Count < max)
                            picked.Add(card);
                    }
                }
                // The opponent's most threatening cards, as many as allowed (cards whose own
                // effect this removal would set off come after the others)...
                foreach (ClientCard card in cards.Where(c => c.Controller == 1 && !picked.Contains(c))
                    .OrderByDescending(c => Threat(c) - (SetsOffOwnEffect(c, kind, false, false) ? SetsOffEffectPenalty : 0)))
                {
                    if (picked.Count >= max)
                        break;
                    picked.Add(card);
                }
                // ...and only as many of our own as we must: first ones that gain from being
                // removed this way (rule 1), then the least valuable.
                bool missesTiming = Duel.CurrentChain.Count > 1;
                foreach (ClientCard card in cards.Where(c => c.Controller != 1 && !picked.Contains(c))
                    .OrderBy(c => SetsOffOwnEffect(c, kind, true, missesTiming) ? 0 : 1)
                    .ThenBy(c => Value(c)))
                {
                    if (picked.Count >= min)
                        break;
                    picked.Add(card);
                }
                if (planUsed)
                    _plan = null;
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
        // Tributes and level sums: Tribute Summons, Ritual Summons, Synchro Summons.
        //
        // These pick the cheapest set of cards that does the job, by the same Value score
        // used for materials and costs, so Tokens and Normal Monsters go before useful
        // Effect Monsters. Tributing one of the opponent's monsters (Soul Exchange, Kaiju,
        // Lava Golem...) counts as a gain, biggest threat first. If our choice is somehow
        // not allowed, WindBot's usual choice is used instead.

        // With more cards to choose from than this, we don't try every combination; we
        // take the cheapest cards first instead (e.g. a Ritual Spell that can use the Deck).
        private const int MaxCardsToTryEveryCombination = 14;

        // What giving up this card costs us.
        private static int TributeCost(ClientCard card)
        {
            return card.Controller == 0 ? Value(card) : -Threat(card);
        }

        public override IList<ClientCard> OnSelectTribute(IList<ClientCard> cards, int min, int max, int hint, bool cancelable)
        {
            if (cards.Count == 0)
                return null;
            List<ClientCard> cheapestFirst = cards.OrderBy(c => TributeCost(c)).ToList();
            List<ClientCard> best = CheapestValidSet(cheapestFirst, picked => CanTotal(picked, 0, 0, min, max));
            if (best != null)
                return best;
            return AI.FindTributeSelection(cheapestFirst, min, max);
        }

        public override IList<ClientCard> OnSelectSum(IList<ClientCard> cards, IList<ClientCard> mandatoryCards,
            int sum, int min, int max, int hint, bool exactEqual)
        {
            // Only Ritual tributes and Synchro Materials; other "total Levels" choices
            // (e.g. sending cards from the Deck) are left to WindBot.
            if (hint != HintMsg.Release && hint != HintMsg.SynchroMaterial)
                return null;
            if (cards.Count == 0)
                return null;
            List<ClientCard> cheapestFirst = cards.OrderBy(c => TributeCost(c)).ToList();
            List<ClientCard> best = CheapestValidSet(cheapestFirst, picked =>
            {
                List<ClientCard> all = mandatoryCards.Concat(picked).ToList();
                if (exactEqual)
                    return picked.Count >= min && picked.Count <= max && CanTotal(all, 0, 0, sum, sum);
                return IsJustEnough(all, sum);
            });
            if (best != null)
                return best;
            return AI.FindSumSelection(cheapestFirst, mandatoryCards, sum, min, max, exactEqual);
        }

        // Tries every combination of the cards and returns the allowed one that costs us
        // least (fewer cards on a tie), or null if none is allowed or there are too many cards.
        private static List<ClientCard> CheapestValidSet(List<ClientCard> cards, Func<List<ClientCard>, bool> allowed)
        {
            if (cards.Count > MaxCardsToTryEveryCombination)
                return null;
            List<ClientCard> best = null;
            long bestCost = 0;
            int combinations = 1 << cards.Count;
            for (int mask = 0; mask < combinations; ++mask)
            {
                List<ClientCard> picked = new List<ClientCard>();
                long cost = 0;
                for (int i = 0; i < cards.Count; ++i)
                {
                    if ((mask & (1 << i)) != 0)
                    {
                        picked.Add(cards[i]);
                        cost += TributeCost(cards[i]);
                    }
                }
                if (best != null && (cost > bestCost || (cost == bestCost && picked.Count >= best.Count)))
                    continue;
                if (!allowed(picked))
                    continue;
                best = picked;
                bestCost = cost;
            }
            return best;
        }

        // True if the cards' Levels (or Tribute counts) can add up to between min and max.
        // Some cards can count as either of two numbers (OpParam1 or OpParam2).
        private static bool CanTotal(IList<ClientCard> cards, int index, long total, long min, long max)
        {
            if (total > max)
                return false;
            if (index == cards.Count)
                return total >= min;
            ClientCard card = cards[index];
            if (CanTotal(cards, index + 1, total + card.OpParam1, min, max))
                return true;
            return card.OpParam2 > 0 && card.OpParam2 != card.OpParam1
                && CanTotal(cards, index + 1, total + card.OpParam2, min, max);
        }

        // "Total Levels equal or more than": the cards must reach the sum, but without any
        // card that isn't needed (the game's rule for these selections).
        private static bool IsJustEnough(IList<ClientCard> cards, int sum)
        {
            if (cards.Count == 0)
                return sum <= 0;
            long lowest = 0;
            long highest = 0;
            int smallestCard = int.MaxValue;
            foreach (ClientCard card in cards)
            {
                int low = card.OpParam2 > 0 ? Math.Min(card.OpParam1, card.OpParam2) : card.OpParam1;
                lowest += low;
                highest += Math.Max(card.OpParam1, card.OpParam2);
                smallestCard = Math.Min(smallestCard, low);
            }
            return highest >= sum && lowest - smallestCard < sum;
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
