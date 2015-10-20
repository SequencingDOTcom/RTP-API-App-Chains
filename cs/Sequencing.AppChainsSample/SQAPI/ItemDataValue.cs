namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// Represents result property value
    /// </summary>
    public class ItemDataValue
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string SubType { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return string.Format("Name: {0}, Title: {1}, SubTitle: {2}, Description: {3}, Type: {4}, SubType: {5}, Value: {6}", Name, Title, SubTitle, Description, Type, SubType, Value);
        }
    }
}