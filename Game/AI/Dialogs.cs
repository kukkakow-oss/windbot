using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

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
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(DialogsData));
            string dialogfilename = game.Dialog;
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
            if (!_game._chat)
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
