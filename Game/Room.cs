namespace WindBot.Game
{
    public class Room
    {
        public bool IsHost { get; set; }
        public string[] Names { get; set; }
        public bool[] IsReady { get; set; }
        public int Position { get; set; }

        public Room()
        {
            Names = new string[11];     // Bot Theater: five-a-side rooms have lobby positions up to 10
            IsReady = new bool[11];
            Position = -1;
        }
    }
}