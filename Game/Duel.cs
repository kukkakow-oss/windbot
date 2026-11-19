using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game
{
    public class Duel
    {
        public bool IsFirst { get; set; }
        public bool IsNewRule { get; set; }
        public bool IsNewRule2020 { get; set; }
        public bool DeckReversed { get; set; }

        public ClientField[] Fields { get; private set; }

        // Bot Theater, four-player free-for-all. Each seat's field is kept by side: 0 is the bot,
        // then the other seats clockwise from it (protocol player numbers are seats 0-3). The AI
        // still sees two players: Fields[0] is the bot (Sides[0]) and Fields[1] is all three
        // opponents together, rebuilt from their sides (see RefreshEnemyView), so "the opponent"
        // (player 1) means any opponent. In two-player duels Sides is Fields.
        public bool IsFourPlayer { get; private set; }
        public int Seat { get; private set; }
        public ClientField[] Sides { get; private set; }
        public bool[] SideOut { get; private set; }

        public int Turn { get; set; }
        public int Player { get; set; }
        public DuelPhase Phase { get; set; }
        public MainPhase MainPhase { get; set; }
        public BattlePhase BattlePhase { get; set; }

        public int LastChainPlayer { get; set; }
        public CardLocation LastChainLocation { get; set; }
        public IList<ClientCard> CurrentChain { get; set; }
        public IList<ChainInfo> CurrentChainInfo { get; set; }
        public IList<ClientCard> ChainTargets { get; set; }
        public IList<ClientCard> LastChainTargets { get; set; }
        public IList<ClientCard> ChainTargetOnly { get; set; }
        public int LastSummonPlayer { get; set; }
        public IList<ClientCard> SummoningCards { get; set; }
        public IList<ClientCard> LastSummonedCards { get; set; }
        public int SolvingChainIndex { get; set; }
        public IList<int> NegatedChainIndexList { get; set; }

        public Duel()
        {
            Fields = new ClientField[2];
            Fields[0] = new ClientField();
            Fields[1] = new ClientField();
            Sides = Fields;
            SideOut = new bool[4];
            SideTeam = new int[] { 0, 1, 2, 3 };
            LastChainPlayer = -1;
            LastChainLocation = 0;
            CurrentChain = new List<ClientCard>();
            CurrentChainInfo = new List<ChainInfo>();
            ChainTargets = new List<ClientCard>();
            LastChainTargets = new List<ClientCard>();
            ChainTargetOnly = new List<ClientCard>();
            LastSummonPlayer = -1;
            SummoningCards = new List<ClientCard>();
            LastSummonedCards = new List<ClientCard>();
            SolvingChainIndex = 0;
            NegatedChainIndexList = new List<int>();
            MainPhase = new MainPhase();
            BattlePhase = new BattlePhase();
            DeckReversed = false;
        }

        public ClientCard GetCard(int player, CardLocation loc, int seq)
        {
            return GetCard(player, (int)loc, seq, 0);
        }

        public ClientCard GetCard(int player, int loc, int seq, int subSeq)
        {
            if (player < 0 || player >= Sides.Length)
                return null;

            bool isXyz = (loc & 0x80) != 0;
            CardLocation location = (CardLocation)(loc & 0x7f);

            IList<ClientCard> cards = null;
            switch (location)
            {
                case CardLocation.Deck:
                    cards = Sides[player].Deck;
                    break;
                case CardLocation.Hand:
                    cards = Sides[player].Hand;
                    break;
                case CardLocation.MonsterZone:
                    cards = Sides[player].MonsterZone;
                    break;
                case CardLocation.SpellZone:
                    cards = Sides[player].SpellZone;
                    break;
                case CardLocation.Grave:
                    cards = Sides[player].Graveyard;
                    break;
                case CardLocation.Removed:
                    cards = Sides[player].Banished;
                    break;
                case CardLocation.Extra:
                    cards = Sides[player].ExtraDeck;
                    break;
            }
            if (cards == null)
                return null;

            if (seq >= cards.Count)
                return null;

            if (isXyz)
            {
                ClientCard card = cards[seq];
                if (card == null || subSeq >= card.Overlays.Count)
                    return null;
                return new ClientCard(card.Overlays[subSeq], CardLocation.Overlay, 0, 0);
            }

            return cards[seq];
        }

        public void AddCard(CardLocation loc, int cardId, int player, int seq, int pos)
        {
            ClientCard card = new ClientCard(cardId, loc, seq, pos);
            AddCard(loc, card, player, seq, pos, cardId);
        }

        public void AddCard(CardLocation loc, ClientCard card, int player, int seq, int pos, int id)
        {
            card.Location = loc;
            card.Sequence = seq;
            card.Position = pos;
            card.Controller = View(player);
            card.SetId(id);
            switch (loc)
            {
                case CardLocation.Hand:
                    Sides[player].Hand.Add(card);
                    ResetSequence(Sides[player].Hand);
                    break;
                case CardLocation.Grave:
                    Sides[player].Graveyard.Add(card);
                    ResetSequence(Sides[player].Graveyard);
                    break;
                case CardLocation.Removed:
                    Sides[player].Banished.Add(card);
                    ResetSequence(Sides[player].Banished);
                    break;
                case CardLocation.MonsterZone:
                    Sides[player].MonsterZone[seq] = card;
                    break;
                case CardLocation.SpellZone:
                    Sides[player].SpellZone[seq] = card;
                    break;
                case CardLocation.Deck:
                    if (seq == 0 && Sides[player].Deck.Count > 0)
                        Sides[player].Deck.Insert(0, card);
                    else
                        Sides[player].Deck.Add(card);
                    ResetSequence(Sides[player].Deck);
                    break;
                case CardLocation.Extra:
                    if (seq >= 0 && seq < Sides[player].ExtraDeck.Count)
                        Sides[player].ExtraDeck.Insert(seq, card);
                    else
                        Sides[player].ExtraDeck.Add(card);
                    ResetSequence(Sides[player].ExtraDeck);
                    break;
            }
        }

        public void RemoveCard(CardLocation loc, ClientCard card, int player, int seq)
        {
            switch (loc)
            {
                case CardLocation.Hand:
                    Sides[player].Hand.Remove(card);
                    ResetSequence(Sides[player].Hand);
                    break;
                case CardLocation.Grave:
                    Sides[player].Graveyard.Remove(card);
                    ResetSequence(Sides[player].Graveyard);
                    break;
                case CardLocation.Removed:
                    Sides[player].Banished.Remove(card);
                    ResetSequence(Sides[player].Banished);
                    break;
                case CardLocation.MonsterZone:
                    Sides[player].MonsterZone[seq] = null;
                    break;
                case CardLocation.SpellZone:
                    Sides[player].SpellZone[seq] = null;
                    break;
                case CardLocation.Deck:
                    Sides[player].Deck.Remove(card);
                    ResetSequence(Sides[player].Deck);
                    break;
                case CardLocation.Extra:
                    Sides[player].ExtraDeck.Remove(card);
                    ResetSequence(Sides[player].ExtraDeck);
                    break;
            }
        }

        private static void ResetSequence(IList<ClientCard> cards)
        {
            for (int i = 0; i < cards.Count; ++i)
                cards[i].Sequence = i;
        }

        /// <summary>
        /// A protocol player number as the AI sees players: 0 the bot, 1 anyone else (four
        /// players: any of the other three, teammates too), 2 no player. For the side a card or
        /// chain belongs to (friend or foe), see View.
        /// </summary>
        public int GetLocalPlayer(int player)
        {
            if (IsFourPlayer)
            {
                int side = GetSide(player);
                return side == 0 ? 0 : (side < 4 ? 1 : 2);
            }
            return IsFirst ? player : 1 - player;
        }

        /// <summary>
        /// Four players, team matches: each side's team (same number = teammates; side 0 is the
        /// bot). In a free-for-all everyone has a team of their own.
        /// </summary>
        public int[] SideTeam { get; private set; }

        /// <summary>Four players: sets each side's team from each seat's.</summary>
        public void SetTeams(int[] seatTeam)
        {
            for (int seat = 0; seat < 4; ++seat)
                SideTeam[GetSide(seat)] = seatTeam[seat];
        }

        /// <summary>Four players: whether that side is the bot's teammate.</summary>
        public bool IsTeammate(int side)
        {
            return IsFourPlayer && side > 0 && side < 4 && SideTeam[side] == SideTeam[0];
        }

        /// <summary>Whether that side is an opponent (four players: on another team).</summary>
        public bool IsOpponent(int side)
        {
            if (!IsFourPlayer)
                return side == 1;
            return side > 0 && side < 4 && SideTeam[side] != SideTeam[0];
        }

        /// <summary>
        /// The side (index into Sides) of a protocol player number. Two players: the same as
        /// GetLocalPlayer. Four players: 0 the bot, 1-3 the other seats clockwise from it, and
        /// 4 for anything that isn't a seat (no player, all players).
        /// </summary>
        public int GetSide(int player)
        {
            if (!IsFourPlayer)
                return IsFirst ? player : 1 - player;
            if (player < 0 || player > 3)
                return 4;
            return (player - Seat + 4) % 4;
        }

        /// <summary>
        /// How the AI sees a side's cards, chains and summons: 0 the bot's own (four players:
        /// a teammate's too, so the AI treats them as friendly), 1 an opponent's (any opponent in
        /// a four-player duel), 2 none of them (a draw, or no player).
        /// </summary>
        public int View(int side)
        {
            if (!IsFourPlayer)
                return side;
            if (side == 0 || IsTeammate(side))
                return 0;
            return (side > 0 && side < 4) ? 1 : 2;
        }

        /// <summary>The protocol player number (seat) of a side.</summary>
        public int SeatOf(int side)
        {
            if (!IsFourPlayer)
                return IsFirst ? side : 1 - side;
            return (side + Seat) % 4;
        }

        public void SetupTwoPlayer()
        {
            IsFourPlayer = false;
            Seat = 0;
            Sides = Fields;
            for (int i = 0; i < SideOut.Length; ++i)
                SideOut[i] = false;
        }

        /// <summary>Four players: the bot sits in seat (0-3); seat 0 goes first.</summary>
        public void SetupFourPlayer(int seat)
        {
            IsFourPlayer = true;
            Seat = seat;
            IsFirst = seat == 0;
            Sides = new ClientField[4];
            Sides[0] = Fields[0];
            for (int i = 1; i < 4; ++i)
                Sides[i] = new ClientField();
            for (int i = 0; i < SideOut.Length; ++i)
                SideOut[i] = false;
            for (int i = 0; i < 4; ++i)
                SideTeam[i] = i;
        }

        /// <summary>
        /// Four players: the first opponent clockwise from the bot who is still in the duel
        /// (the side used for the "opponent's" half of a zone choice). 1 in two-player duels.
        /// </summary>
        public int NextOpponentSide()
        {
            if (IsFourPlayer)
            {
                for (int side = 1; side < 4; ++side)
                    if (!SideOut[side] && IsOpponent(side))
                        return side;
            }
            return 1;
        }

        /// <summary>
        /// Four players: rebuilds Fields[1] (what the AI sees as "the opponent") from the fields
        /// of the opponents still in the duel. Nothing to do in two-player duels.
        /// </summary>
        public void RefreshEnemyView()
        {
            if (!IsFourPlayer || Fields[1].Hand == null)
                return;
            List<ClientField> opponents = new List<ClientField>();
            for (int side = 1; side < 4; ++side)
            {
                if (!SideOut[side] && IsOpponent(side) && Sides[side].Hand != null)
                    opponents.Add(Sides[side]);
            }
            Fields[1].MergeFrom(opponents);
        }

        /// <summary>Clears every side's "under attack" mark (and the combined opponent's).</summary>
        public void ClearUnderAttack()
        {
            foreach (ClientField field in Sides)
                field.UnderAttack = false;
            Fields[1].UnderAttack = false;
        }

        /// <summary>Clears every side's battling monster and "under attack" mark.</summary>
        public void ClearBattle()
        {
            foreach (ClientField field in Sides)
            {
                field.BattlingMonster = null;
                field.UnderAttack = false;
            }
            Fields[1].BattlingMonster = null;
            Fields[1].UnderAttack = false;
        }

        /// <summary>
        /// Returns the newest chain link while the chain is being built, including
        /// while that link is selecting its activation cost or targets.
        /// Unlike GetCurrentSolvingChainCard, this returns null once chain resolution
        /// has started.
        /// </summary>
        public ClientCard GetCurrentChainCard()
        {
            if (SolvingChainIndex != 0 || CurrentChain.Count == 0) return null;
            return CurrentChain[CurrentChain.Count - 1];
        }

        /// <summary>
        /// Returns the chain link currently being resolved. This is null before the
        /// engine starts resolving the chain, including during activation target selection.
        /// </summary>
        public ClientCard GetCurrentSolvingChainCard()
        {
            if (SolvingChainIndex == 0 || SolvingChainIndex > CurrentChain.Count) return null;
            return CurrentChain[SolvingChainIndex - 1];
        }

        public ChainInfo GetCurrentSolvingChainInfo()
        {
            if (SolvingChainIndex == 0 || SolvingChainIndex > CurrentChainInfo.Count) return null;
            return CurrentChainInfo[SolvingChainIndex - 1];
        }

        public bool IsCurrentSolvingChainNegated()
        {
            return SolvingChainIndex > 0 && NegatedChainIndexList.Contains(SolvingChainIndex);
        }
    }
}
