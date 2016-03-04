using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.IO;
//using Microsoft.Speech.Synthesis;//Win7 Ver.
using System.Speech.Synthesis;//Win8/10 Ver.
using System.Text.RegularExpressions;
// System.Uri;

namespace Twitch_Bouyomi
{

    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private string current_channel;
        private string current_OAuth;
        private IrcClient irc;
        private string Talker;

        private int Speech_Volume = 100;
        private int Speech_Rate = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void change_channel_ID(string id)
        {
            current_channel = id;
            current_ID_text.Content = id;
        }

        private void MainWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            string close_msg = "關閉棒讀醬(´・ω・`)？";

            MessageBoxResult result =       //ttps://msdn.microsoft.com/ja-jp/library/system.windows.window.closing%28v=vs.110%29.aspx
              MessageBox.Show(
                close_msg,
                "Data App",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.No)
            {
                // If user doesn't want to close, cancel closure
                e.Cancel = true;
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void change_channel(object sender, RoutedEventArgs e)   //當Login視窗被呼叫.
        {

            string path = "accpass.txt";
            string file_acc = null;
            FileStream Fstream;
            Fstream = new FileStream(path, FileMode.OpenOrCreate);

            StreamReader FileRead = new StreamReader(Fstream);
            StreamWriter FileWrite = new StreamWriter(Fstream);
            //FileWrite.Flush();

            
            Login cc = new Login();
            file_acc = FileRead.ReadLine();
            if (file_acc != null)
            {
                
                Fstream.Position = 0;   //return to the Beginning of the FileStream.
                /*
                file_acc = FileRead.ReadLine();
                file_oa = FileRead.ReadLine();
                
                file_acc.Replace("\r\n", "");
                file_oa.Replace("\r\n", "");
                */
                cc.OAuth.Clear();
                cc.OAuth.AppendText(FileRead.ReadLine());
                cc.Channel_Account.Clear();
                cc.Channel_Account.AppendText(FileRead.ReadLine());
            }
            
            if (cc.ShowDialog() == true)    //當按下確定按鈕.
            {
                if (cc.Channel_Account.Text != null)    
                {
                    current_channel = cc.Channel_Account.Text;
                    current_OAuth = cc.OAuth.Text;
                    current_ID_text.Content = current_channel;

                    //儲存登入資訊
                    Fstream.Position = 0;   //return to the Beginning of the FileStream.
                    FileWrite.WriteLine(current_channel);
                    FileWrite.WriteLine(current_OAuth);
                    FileWrite.Flush();
                    
                    //開始IRC的工作
                    StartSession();
                }
            }
            Fstream.Close();    //關閉檔案串流.
        }
        
        private void StartSession()
        {
            string msg;
            irc = new IrcClient("irc.twitch.tv", 6667, current_channel, current_OAuth);
            msg = irc.readMessage();
            if (msg.Contains("Error"))
            {
                MessageBox.Show("帳號或OAuth有誤，登入失敗。");
            }
            else
            {
                Thread IRCRoom = new Thread(Enter_IRC_Room);
                IRCRoom.IsBackground = true;     //如此使得main thread 結束後子thread 也可以跟著被關閉.
                IRCRoom.Start();
            }
        }

        //用thread的方式建立，並與Twitch伺服器做溝通.
        private void Enter_IRC_Room()
        {
            Push_A_message_to_Room("***Ver.0.4 START.\n");
            Push_A_message_to_Room("***已登入Twitch IRC Room，棒讀開始.\n");
            while (true)
            {
                string msg = irc.readMessage();
                if (msg.Contains("PING") && !msg.Contains("RIVMSG"))
                {
                    irc.sendMessage("PONG tmi.twitch.tv\r\n");
                }
                else
                {
                    if (msg.Contains("RIVMSG"))
                    {
                        msg = Room_Content_Proccess(msg);   //回傳的msg只剩下觀眾留言的訊息.
                        Speech_Content_Proccess(msg);
                    }
                }

            }
        }

        private void Push_A_message_to_Room(string msg)
        {
            if (IRC_textRoom.Dispatcher.CheckAccess())
            {
                IRC_textRoom.AppendText(msg);
                IRC_textRoom.ScrollToEnd();
            }
            else
            {
                IRC_textRoom.Dispatcher.BeginInvoke((Action)(() => 
                {
                    IRC_textRoom.AppendText(msg);
                    IRC_textRoom.ScrollToEnd();
                }));
            }
        }

        private void SpeechTheText(string msg)
        {
            //tps://msdn.microsoft.com/en-us/library/system.speech.synthesis.speechsynthesizer%28v=vs.110%29.aspx
            // Initialize a new instance of the SpeechSynthesizer.
            SpeechSynthesizer synth = new SpeechSynthesizer();

            if (msg.Length >= 26)
            {
                string s = "以下略\n";
                msg = msg.Remove(20);
                msg = string.Concat(msg, s);
            }

            // Configure the audio output. 
            //Win7 Verson.===================================
            /*if (IsContainsJapanese(msg))
            {
                synth.SelectVoice("Microsoft Server Speech Text to Speech Voice (ja-JP, Haruka)");
            }
            else
            {
                synth.SelectVoice("Microsoft Server Speech Text to Speech Voice (zh-TW, HanHan)");
            }*/
            //===============================================
            //Win8/10 Ver.===================================
            if (IsContainsJapanese(msg))
            {
                synth.SelectVoice("Microsoft Haruka Desktop");
            }
            else
            {
                synth.SelectVoice("Microsoft Hanhan Desktop");
            }
            //===============================================

            synth.Volume = Speech_Volume;
            synth.Rate = Speech_Rate;
            // Speak the string.
            synth.SetOutputToDefaultAudioDevice();
            synth.Speak(msg);
        }

        private string Room_Content_Proccess(string msg)
        {
            string Listener_msg = null;
            string[] temp;
            int index;
            //the example from irc server is
            //:abcd!abcd@abcd.tmi.twitch.tv PRIVMSG #StreamerChannel : TestMessageFromListener
            Talker = null;
            index = msg.IndexOf('!') + 1;
            msg = msg.Substring(index);

            temp = msg.Split('@');
            Talker = temp[0];

            index = msg.IndexOf(':') + 1;
            Listener_msg = msg.Substring(index);
            Push_A_message_to_Room(Talker + " : " + Listener_msg);
            return Listener_msg;
        }

        private void Speech_Content_Proccess(string msg)
        {
            string pattern = @"^(http|https|ftp|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
            msg = Emotion(msg);
            if(msg.Contains("http:/")|| msg.Contains("Http:/")|| msg.Contains("HTTP:/"))
            {
                msg = "有連結";
            }
            
            if (msg.Contains("."))
            {
                if (!msg.StartsWith(".."))
                {
                    Regex reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    if (reg.IsMatch(msg))
                    {
                        msg = "有連結";
                    }
                }
            }
            SpeechTheText(msg);
        }

        private string Emotion(string msg)
        {
            if (msg == "...\n") 
            {
                msg = msg.Replace("...", "覺得無言");
            }
            if (msg.Contains("0.0") || msg.Contains("o.o"))
            {
                msg = msg.Replace("0.0", "表情盯著你看");
                msg = msg.Replace("o.o", "表情盯著你看");
            }
            if(msg.Contains("O.o") || msg.Contains("o.O") ||
                    msg.Contains("O_o") || msg.Contains("o_O"))
            {
                msg = msg.Replace("O.o", "表情有點驚訝");
                msg = msg.Replace("o.O", "表情有點驚訝");
                msg = msg.Replace("O_o", "表情有點驚訝");
                msg = msg.Replace("o_O", "表情有點驚訝");
            }
            if (msg.Contains("3.3"))
            {
                msg = msg.Replace("3.3", "表情看不太到");
            }
            if (msg.Contains("030") || msg.Contains("=3="))
            {
                msg = msg.Replace("030", "表情嘟嘴");
                msg = msg.Replace("=3=", "表情嘟嘴");
            }
            if (msg.Contains("= =") || msg.Contains("=.=") || msg.Contains("=_="))
            {
                msg = msg.Replace("= =", "表情無解");
                msg = msg.Replace("=.=", "表情無解");
                msg = msg.Replace("=_=", "表情無解");
            }
            while (msg.Contains("www") || msg.Contains("WWW") || msg.Contains("ｗｗｗ") || msg.Contains("ＷＷＷ"))
            {
                msg = msg.Replace("www", "walawala");
                msg = msg.Replace("WWW", "walawala");
                msg = msg.Replace("ｗｗｗ", "walawala");
                msg = msg.Replace("ＷＷＷ", "walawala");
            }
            while(msg.Contains("ww")|| msg.Contains("WW")|| msg.Contains("ｗｗ")|| msg.Contains("ＷＷ"))
            {
                msg = msg.Replace("ww", "wala");
                msg = msg.Replace("WW", "wala");
                msg = msg.Replace("ｗｗ", "wala");
                msg = msg.Replace("ＷＷ", "wala");
            }
            while (msg.Contains("哈哈哈") || msg.Contains("呵呵呵") || msg.Contains("顆顆顆")||msg.Contains("嘻嘻嘻"))
            {
                msg = msg.Replace("哈哈哈", "哈");
                msg = msg.Replace("呵呵呵", "呵");
                msg = msg.Replace("顆顆顆", "顆");
                msg = msg.Replace("嘻嘻嘻", "嘻");
            }
            while(msg.Contains("~")|| msg.Contains("～"))
            {
                msg = msg.Replace("~", "");
                msg = msg.Replace("～", "");
            }

            return msg;
        }

        private static IEnumerable<char> GetCharsInRange(string text, int min, int max)
        {
            return text.Where(e => e >= min && e <= max);
        }

        public static bool IsContainsJapanese(string text)
        {
            //var romaji = GetCharsInRange(text, 0x0020, 0x007E).Any();
            var hiragana = GetCharsInRange(text, 0x3040, 0x309F).Any(); //平仮名
            var katakana = GetCharsInRange(text, 0x30A0, 0x30FF).Any(); //片仮名
            //var kanji = GetCharsInRange(text, 0x4E00, 0x9FBF).Any();
            return hiragana || katakana;
        }


        private void Set_the_change_Click(object sender, RoutedEventArgs e)
        {
            Speech_Volume = Int32.Parse(WPF_Volume_slider_box.Text);
            Speech_Rate = Int32.Parse(WPF_Rate_slider_box.Text);
        }
    }
    //===================End of MainWindow Class.======================







    public class IrcClient
    {
        public NetworkStream stream;
        private TcpClient tcpClient;
        private StreamReader inputStream;
        private StreamWriter outputStream;
        private string userName;
        private string channel;

        public IrcClient(string ip, int port, string userName, string password)
        {
            this.userName = userName;
            tcpClient = new TcpClient(ip, port);
            stream = tcpClient.GetStream();
            inputStream = new StreamReader(tcpClient.GetStream());
            outputStream = new StreamWriter(tcpClient.GetStream());

            if (!password.StartsWith("oauth:"))
            {
                password = "oauth:" + password;
            }

            outputStream.WriteLine("PASS " + password);
            outputStream.WriteLine("NICK " + userName);
            //outputStream.WriteLine("USER" + userName + " 8 * :" + userName);
            outputStream.Flush();
            joinRoom(userName);
        }

        public void joinRoom(string channel)
        {
            channel = channel.ToLower();
            outputStream.WriteLine("JOIN #" + channel);
            //outputStream.WriteLine("JOIN #" + "TestChannel");
            outputStream.Flush();
        }

        public void sendMessage(string message)
        {
            outputStream.WriteLine(message);
            outputStream.Flush();
        }

        public string readMessage()
        {
            string message = inputStream.ReadLine() + "\n";
            return message;
        }

    }
    //=============End of IrcClient Class=============
    
}