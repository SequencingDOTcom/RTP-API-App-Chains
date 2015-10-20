namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// Appchain job parameter holder
    /// </summary>
    public class NewJobParameter
    {
        public NewJobParameter(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public NewJobParameter(string value)
        {
            Value = value;
        }

        public NewJobParameter(long? val)
        {
            ValueLong = val;
        }


        public NewJobParameter()
        {
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public long? ValueLong { get; set; }
    }
}