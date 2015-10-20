namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// Class that represents result entity if plain text string
    /// </summary>
    class TextResultValue : ResultValue
    {
        private string data;

        public TextResultValue(string data): base(ResultType.TEXT)
        {
            this.data = data;
        }

        public string Data
        {
            get { return data; }
        }
    }
}