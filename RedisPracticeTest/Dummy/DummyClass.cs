using System;

namespace RedisPracticeTest.Dummy
{
    public class DummyClass
    {
        public DummyClass()
        {
            StringValue = Generator.RandomString(8);

            IntValue = Generator.RandomInt(1000);

            BoolValue = Generator.RandomBool();

            DateValue = Generator.RandomDate();
        }

        public string StringValue { get; set; }

        public int IntValue { get; set; }

        public bool BoolValue { get; set; }

        public DateTime DateValue { get; set; }

        public DateTime? NullValue => null;

    }
}
