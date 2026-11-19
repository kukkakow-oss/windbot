using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;

namespace WindBot.Game.AI
{
    [DataContract]
    public class DialogsData
    {
        [DataMember]
        public string[] welcome { get; set; }
        [DataMember]
        public string[] deckerror { get; set; }
        [DataMember]
        public string[] duelstart { get; set; }
        [DataMember]
        public string[] newturn { get; set; }
        [DataMember]
        public string[] endturn { get; set; }
        [DataMember]
        public string[] directattack { get; set; }
        [DataMember]
        public string[] attack { get; set; }
        [DataMember]
        public string[] ondirectattack { get; set; }
        [DataMember]
        public string facedownmonstername { get; set; }
        [DataMember]
        public string[] activate { get; set; }
        [DataMember]
        public string[] summon { get; set; }
        [DataMember]
        public string[] setmonster { get; set; }
        [DataMember]
        public string[] chaining { get; set; }
        [DataMember]
        public string[] surrender { get; set; }
        [DataMember]
        public string[] custom { get; set; }
    }
    public class Dialogs
    {
        private GameClient _game;

        private string[] _welcome;
        private string[] _deckerror;
        private string[] _duelstart;
        private string[] _newturn;
        private string[] _endturn;
        private string[] _directattack;
        private string[] _attack;
        private string[] _ondirectattack;
        private string _facedownmonstername;
        private string[] _activate;
        private string[] _summon;
        private string[] _setmonster;
        private string[] _chaining;
        private string[] _surrender;
        private string[] _custom;

        // Bot Theater: what goes in the attack lines. {0} is the attacking monster, {1} the monster
        // attacked (for a direct attack, the player attacked) and {2} the player attacked or the
        // player whose monster is attacked; in our "attacked directly" line, {1} is us and {2}
        // the player who attacked. In two-player and tag duels the game doesn't say who
        // is playing, so these two are set from the room (the opponent now playing, and the
        // player now playing on our side).
        public string OpponentName { get; set; }
        public string OwnName { get; set; }

        // Bot Theater, four-player free-for-all: the game shows what a bot says in a speech bubble
        // from its portrait, and the bots only say these: a summon line the first time they summon
        // a monster in a turn, a "welcome" line the first time they attack directly in a turn or
        // an "attack" line if that first attack is on a monster, an "attacked directly" line when
        // they're knocked out, and a "direct attack" line when they win.
        public bool FourPlayer { get; set; }
        private bool _summonedThisTurn;
        private bool _attackedThisTurn;

        public void OnNewTurnFourPlayer()
        {
            _summonedThisTurn = false;
            _attackedThisTurn = false;
        }

        // The first attack we declare in a turn. target: the monster attacked (null if it's face-down);
        // targetPlayer: the player attacked, or the one whose monster is attacked.
        public void SendFirstAttack(string attacker, string target, string targetPlayer, bool direct)
        {
            if (_attackedThisTurn)
                return;
            _attackedThisTurn = true;
            if (direct)
                SendFourPlayerLine(_welcome, attacker, targetPlayer, targetPlayer);
            else
                SendFourPlayerLine(_attack, attacker, string.IsNullOrEmpty(target) ? _facedownmonstername : target, targetPlayer);
        }

        // We're knocked out; attacker: what attacked us directly (null if not knocked out that way).
        public void SendKnockedOut(string attacker, string self)
        {
            SendFourPlayerLine(_ondirectattack, attacker, self, self);
        }

        // We've won; lastOpponent: the last player knocked out.
        public void SendVictory(string monster, string lastOpponent)
        {
            SendFourPlayerLine(_directattack, monster, lastOpponent, lastOpponent);
        }

        // A random line from the list, with args in {0}, {1}... If something is missing (no card
        // name, say), a line that doesn't need it if there is one; else the word for a face-down
        // monster in {0} and "you" for the rest.
        private void SendFourPlayerLine(IList<string> array, params string[] args)
        {
            if (!_game._chat || array == null || array.Count == 0)
                return;
            List<string> pool = array.Where(line => HasEverything(line, args)).ToList();
            if (pool.Count == 0)
                pool = array.ToList();
            object[] filled = new object[args.Length];
            for (int i = 0; i < args.Length; ++i)
                filled[i] = !string.IsNullOrEmpty(args[i]) ? args[i] : (i == 0 ? _facedownmonstername : "you");
            string message = PickLine(pool, filled);
            if (!string.IsNullOrEmpty(message))
                _game.Chat(char.ToUpper(message[0]) + message.Substring(1));
        }

        private static readonly Regex Placeholder = new Regex(@"(?<!\{)\{(\d+)");

        // Whether every {n} in the line has something to go in it.
        private static bool HasEverything(string line, string[] args)
        {
            foreach (Match match in Placeholder.Matches(line))
            {
                int index = int.Parse(match.Groups[1].Value);
                if (index >= args.Length || string.IsNullOrEmpty(args[index]))
                    return false;
            }
            return true;
        }

        // A random line from the list with its {0}, {1}... filled in. Lines that can't be filled in
        // (one with a {3} where there's nothing for it, or a stray brace) are skipped instead of
        // stopping the bot with an error.
        private static string PickLine(IList<string> array, object[] opts)
        {
            if (array == null || array.Count == 0)
                return null;
            List<string> usable = new List<string>();
            foreach (string line in array)
            {
                string message = FormatLine(line, opts);
                if (message != null)
                    usable.Add(message);
            }
            if (usable.Count == 0)
                return null;
            return usable[Program.Rand.Next(usable.Count)];
        }

        private static string FormatLine(string line, object[] opts)
        {
            if (line == null)
                return null;
            try
            {
                return string.Format(line, opts);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public Dialogs(GameClient game)
        {
            _game = game;
            Load(game.Dialog);
        }

        /// <summary>
        /// Bot Theater, four-player free-for-all: the lines come from Dialogs/4ffa.json when that
        /// file is there (else from the usual dialog file).
        /// </summary>
        public void UseFourPlayerLines()
        {
            const string fourPlayerFile = "4ffa";
            if (File.Exists(Path.Combine("Dialogs", fourPlayerFile + ".json")))
                Load(fourPlayerFile);
        }

        private void Load(string dialogfilename)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DialogsData));
            using (FileStream fs = Program.ReadFile("Dialogs", dialogfilename, "json"))
            {
                DialogsData data = (DialogsData)serializer.ReadObject(fs);
                _welcome = data.welcome;
                _deckerror = data.deckerror;
                _duelstart = data.duelstart;
                _newturn = data.newturn;
                _endturn = data.endturn;
                _directattack = data.directattack;
                _attack = data.attack;
                _ondirectattack = data.ondirectattack;
                _facedownmonstername = data.facedownmonstername;
                _activate = data.activate;
                _summon = data.summon;
                _setmonster = data.setmonster;
                _chaining = data.chaining;
                _surrender = data.surrender;
                _custom = data.custom;
            }
        }

        public void SendSorry()
        {
            InternalSendMessageForced(new[] { "Sorry, an error occurs." });
        }

        public void SendDeckSorry(string card)
        {
            if (card == "DECK")
                InternalSendMessageForced(new[] { "Deck illegal. Please check the database of your YGOPro and WindBot." });
            else
                InternalSendMessageForced(_deckerror, card);
        }

        public void SendWelcome()
        {
            InternalSendMessage(_welcome);
        }

        public void SendDuelStart()
        {
            InternalSendMessage(_duelstart);
        }

        public void SendNewTurn()
        {
            InternalSendMessage(_newturn);
        }

        public void SendEndTurn()
        {
            InternalSendMessage(_endturn);
        }

        // Bot Theater: the direct-attack lines (attacking directly, or being attacked directly)
        // are only said this often, in percent, so they don't repeat every turn.
        private const int DirectAttackChatChance = 50;

        private static bool ChanceToSay(int percent)
        {
            return Program.Rand.Next(100) < percent;
        }

        public void SendDirectAttack(string attacker)
        {
            if (!ChanceToSay(DirectAttackChatChance))
                return;
            string target = OpponentName ?? "you";
            InternalSendMessage(_directattack, attacker, target, target);
        }

        public void SendAttack(string attacker, string defender)
        {
            if (defender=="monster")
            {
                defender = _facedownmonstername;
            }
            InternalSendMessage(_attack, attacker, defender, OpponentName ?? "you");
        }

        public void SendOnDirectAttack(string attacker)
        {
            if (!ChanceToSay(DirectAttackChatChance))
                return;
            if (string.IsNullOrEmpty(attacker))
            {
                attacker = _facedownmonstername;
            }
            // {1} is us (the player attacked), {2} the player who attacked
            InternalSendMessage(_ondirectattack, attacker, OwnName ?? "me", OpponentName ?? "you");
        }
        public void SendOnDirectAttack()
        {
            if (!ChanceToSay(DirectAttackChatChance))
                return;
            InternalSendMessage(_ondirectattack);
        }

        public void SendActivate(string spell)
        {
            InternalSendMessage(_activate, spell);
        }

        public void SendSummon(string monster)
        {
            if (FourPlayer)
            {
                // free-for-all: only the first summon of each turn
                if (_summonedThisTurn)
                    return;
                _summonedThisTurn = true;
                SendFourPlayerLine(_summon, monster);
                return;
            }
            InternalSendMessage(_summon, monster);
        }

        public void SendSetMonster()
        {
            InternalSendMessage(_setmonster);
        }

        public void SendChaining(string card)
        {
            InternalSendMessage(_chaining, card);
        }

        public void SendSurrender()
        {
            InternalSendMessage(_surrender);
        }

        private void InternalSendMessage(IList<string> array, params object[] opts)
        {
            if (!_game._chat || FourPlayer)
                return;
            string message = PickLine(array, opts);
            if (!string.IsNullOrEmpty(message))
                _game.Chat(message);
        }

        private void InternalSendMessageForced(IList<string> array, params object[] opts)
        {
            string message = PickLine(array, opts);
            if (!string.IsNullOrEmpty(message))
                _game.Chat(message);
        }

        public void SendCustomChat(int index, params object[] opts)
        {
            if (!_game._chat || _custom == null || index < 0 || index >= _custom.Length)
                return;
            string message = FormatLine(_custom[index], opts);
            if (!string.IsNullOrEmpty(message))
                _game.Chat(message);
        }
    }
}
