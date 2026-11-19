using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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

        // Bot Theater, four-player free-for-all: the game shows what a bot says in a speech bubble
        // from its portrait, and the bots only say three things: a summon line the first time they
        // summon a monster in a turn, an "attacked directly" line when they're knocked out, and a
        // "direct attack" line when they win.
        public bool FourPlayer { get; set; }
        private bool _summonedThisTurn;

        public void OnNewTurnFourPlayer()
        {
            _summonedThisTurn = false;
        }

        public void SendKnockedOut(string attacker)
        {
            SendFourPlayerLine(_ondirectattack, attacker);
        }

        public void SendVictory(string monster)
        {
            SendFourPlayerLine(_directattack, monster);
        }

        // A random line from the list. Without a card name to put in it, one that doesn't need a
        // name if there is one (else the word for a face-down monster).
        private void SendFourPlayerLine(IList<string> array, string name)
        {
            if (!_game._chat || array == null || array.Count == 0)
                return;
            IList<string> pool = array;
            if (string.IsNullOrEmpty(name))
            {
                List<string> plain = array.Where(line => !line.Contains("{0}")).ToList();
                if (plain.Count > 0)
                    pool = plain;
                name = _facedownmonstername;
            }
            string message = string.Format(pool[Program.Rand.Next(pool.Count)], name);
            if (message != "")
                _game.Chat(char.ToUpper(message[0]) + message.Substring(1));
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
            InternalSendMessage(_directattack, attacker);
        }

        public void SendAttack(string attacker, string defender)
        {
            if (defender=="monster")
            {
                defender = _facedownmonstername;
            }
            InternalSendMessage(_attack, attacker, defender);
        }

        public void SendOnDirectAttack(string attacker)
        {
            if (!ChanceToSay(DirectAttackChatChance))
                return;
            if (string.IsNullOrEmpty(attacker))
            {
                attacker = _facedownmonstername;
            }
            InternalSendMessage(_ondirectattack, attacker);
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
            if (array == null || array.Count == 0)
                return;
            string message = string.Format(array[Program.Rand.Next(array.Count)], opts);
            if (message != "")
                _game.Chat(message);
        }

        private void InternalSendMessageForced(IList<string> array, params object[] opts)
        {
            string message = string.Format(array[Program.Rand.Next(array.Count)], opts);
            if (message != "")
                _game.Chat(message);
        }

        public void SendCustomChat(int index, params object[] opts)
        {
            if (!_game._chat || _custom == null)
                return;
            string message = string.Format(_custom[index], opts);
            if (message != "")
                _game.Chat(message);
        }
    }
}
