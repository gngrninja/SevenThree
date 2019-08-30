using System;

namespace SevenThree.Modules
{
    public class QuizHelper
    {
        public string GetNumberEmojiFromInt(int number)
        {
            string numberEmoji = string.Empty;
            switch (number)
            {
                case 1:
                {
                    numberEmoji = ":one:";
                    break;
                }
                case 2:
                {
                    numberEmoji = ":two:";
                    break;
                }
                case 3:
                {
                    numberEmoji = ":three:";
                    break;
                }
                case 4:
                {
                    numberEmoji = ":four:";
                    break;
                }
                case 5:
                {
                    numberEmoji = ":five:";
                    break;
                }
                case 6:
                {
                    numberEmoji = ":six:";
                    break;
                }
                case 7:
                {
                    numberEmoji = ":seven:";
                    break;
                }
                case 8:
                {
                    numberEmoji = ":eight:";
                    break;
                }
                case 9:
                {
                    numberEmoji = ":nine:";
                    break;
                }
                case 10:
                {
                    numberEmoji = ":one::zero:";
                    break;
                }
                case 11:
                {
                    numberEmoji = ":one::one:";
                    break;
                }
                case 12:
                {
                    numberEmoji = ":one::two:";
                    break;
                }
                case 13:
                {
                    numberEmoji = ":one::three:";
                    break;
                }
                case 14:
                {
                    numberEmoji = ":one::four:";
                    break;
                }
                default:
                {
                    numberEmoji = ":zero:";
                    break;
                }                                                                                                                                                                                
            }
            return numberEmoji;
        } 

        public string GetPassFail(decimal percentage)
        {
            var emoji = string.Empty;            
            if (percentage >= 74)
            {                
                emoji = ":white_check_mark:";
            }
            else
            {
                emoji = ":no_entry_sign:";
            }
            return emoji;
        }        
    }
}