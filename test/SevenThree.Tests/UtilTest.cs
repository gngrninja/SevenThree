
using System;
using Xunit;
using SevenThree.Modules; 

namespace SevenThree.Tests
{
    public class UtilTest
    {
        [Fact]
        public void EmjoiFromNumber()
        {
            // arrange
            var qz = new QuizHelper();

            // act
            var one = qz.GetNumberEmojiFromInt(1);
            var two = qz.GetNumberEmojiFromInt(2);
            var three = qz.GetNumberEmojiFromInt(3);
            var four = qz.GetNumberEmojiFromInt(4);
            var five = qz.GetNumberEmojiFromInt(5);
            var six = qz.GetNumberEmojiFromInt(6);
            var seven = qz.GetNumberEmojiFromInt(7);
            var eight = qz.GetNumberEmojiFromInt(8);
            var nine = qz.GetNumberEmojiFromInt(9);
            var zero = qz.GetNumberEmojiFromInt(0);
            var ten = qz.GetNumberEmojiFromInt(10);
            var eleven = qz.GetNumberEmojiFromInt(11);
            var twelve = qz.GetNumberEmojiFromInt(12);
            var thirteen = qz.GetNumberEmojiFromInt(13);
            var fourteen = qz.GetNumberEmojiFromInt(14);

            // assert
            Assert.Equal(":one:", one);
            Assert.Equal(":two:", two);
            Assert.Equal(":three:", three);
            Assert.Equal(":four:", four);
            Assert.Equal(":five:", five);
            Assert.Equal(":six:", six);
            Assert.Equal(":seven:", seven);
            Assert.Equal(":eight:", eight);
            Assert.Equal(":nine:", nine);
            Assert.Equal(":zero:", zero);
            Assert.Equal(":one::zero:", ten);
            Assert.Equal(":one::one:", eleven);
            Assert.Equal(":one::two:", twelve);
            Assert.Equal(":one::three:", thirteen);
            Assert.Equal(":one::four:", fourteen);
        }

        [Fact]
        public void PassFailEmoji()
        {
            //arrange
            var qz = new QuizHelper();

            //act
            var pass = qz.GetPassFail(74);
            var fail = qz.GetPassFail(73);

            //assert
            Assert.Equal(":white_check_mark:", pass);
            Assert.Equal(":no_entry_sign:", fail);
        }
    }
}
