using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Net.Security;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using MimeKit;
using MailKit.Net.Smtp;
using Windows.UI;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace GarMon.App
{
    public sealed partial class MainPage : Page
    {
        int ticks = 0;
        int checks = 0;
        bool open = false;
        //string status = "Closed";
        bool sent = false;

        DispatcherTimer timer = new DispatcherTimer();
        public MainPage()
        {
            this.InitializeComponent();

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerTick;
            timer.Start();
        }

        private void TimerTick(object sender, object e)
        {
            UpdateBoard();

            ticks++;
            if (ticks >= 60)
            {
                ticks = 0;
                CheckDoor();
            }
        }

        private void CheckDoor()
        {


            //Check if sensor senses door

            open = true;
            //or
            //open = false;

            if (open)
            {
                HandleOpen();
            }
            else
            {
                checks = 0;
                sent = false;
            }
        }

        private void HandleOpen()
        {
            checks++;
            open = true;
            //status = ":::OPEN:::";

            if (checks > 0 && !sent)
            {
                //SendEmail();
                sent = true;
            }
        }

        private void UpdateBoard()
        {
            txbTimer.Text = (60 - ticks).ToString();
            txbChecks.Text = "Checks : " + checks.ToString();

            if (!open)
            {
                txbStatus.Text = "Status : Closed";
                txbStatus.Foreground = new SolidColorBrush(Colors.DarkCyan);
            }
            else
            {
                txbStatus.Text = "Status :::OPEN:::";
                txbStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }

            if (!open && !sent)
                txbEStatus.Text = "";
            else if (open && !sent)
                txbEStatus.Text = $"Not Sent: {5 - checks} left";
            else
                txbEStatus.Text = "Email :::SENT:::";


        }

        private void SendEmail()
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Joey Tribbiani", "jbbuchanan266@gmail.com"));
            message.To.Add(new MailboxAddress("Mrs. Chanandler Bong", "jbbuchanan266@gmail.com"));
            message.Subject = "How you doin'?";

            message.Body = new TextPart("plain")
            {
                Text = @"Hey Chandler,I just wanted to let you know that Monica and I were going to go play some paintball, you in?-- Joey"
            };

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587);


                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");

                // Note: only needed if the SMTP server requires authentication
                client.Authenticate("YOUR_GMAIL_NAME", "YOUR_PASSWORD");

                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
