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
using Windows.Devices.Gpio;
using Windows.UI.Core;

namespace GarMon.App
{
    public sealed partial class MainPage : Page
    {
        int ticks = 0;
        int checks = 0;
        bool open = false;
        bool sent = false;

        GpioController gpio;
        GpioPin pin5;

        DispatcherTimer timer = new DispatcherTimer();

        public MainPage()
        {
            this.InitializeComponent();

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerTick;
            timer.Start();

            SetupGPIOPins();
        }

        private void TimerTick(object sender, object e)
        {
            ticks++;
            UpdateBoard();

            if (ticks >= 60)
            {
                ticks = 0;
                CheckDoor();
                UpdateSQL();
            }
        }

        private void SetupGPIOPins()
        {
            gpio = GpioController.GetDefault();

            if (gpio == null)
                return; // GPIO not available on this system

            using (pin5 = gpio.OpenPin(5))
            {
                // Latch HIGH value first. This ensures a default value when the pin is set as output
                pin5.Write(GpioPinValue.High);

                // Set the IO direction as input
                pin5.SetDriveMode(GpioPinDriveMode.Input);

                string read = pin5.Read().ToString();

            } // Close pin - will revert to its power-on state
        }



        private void CheckDoor()
        {
            //Check if sensor senses door
            //https://tutorials-raspberrypi.com/raspberry-pi-ultrasonic-sensor-hc-sr04/
            //https://github.com/microsoft/Windows-universal-samples/blob/master/Samples/ProximitySensor/cs/Scenario1_DataEvents.xaml.cs

            //magnet sensors: https://tutorials-raspberrypi.com/raspberry-pi-door-window-sensor-with-reed-relais/

            //open = true;
            //or
            //open = false;


            if (open)
            {
                HandleOpen();
            }
            else
            {
                open = false;
                checks = 0;
                sent = false;
            }
        }

        private void HandleOpen()
        {
            checks++;

            if (checks > 10 && !sent)
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

            if (pin5 == null)
                txbSensorStatus.Text = "Offline";
            else
                txbSensorStatus.Text = "Online";

        }

        private void UpdateSQL()
        {

        }

        private void SendEmail()
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Joey Tribbiani", "jbbuchanan266@gmail.com"));
            message.To.Add(new MailboxAddress("Mrs. Chanandler Bong", "jbbuchanan266@gmail.com"));
            message.Subject = $"Garage Door: {DateTime.Now}";

            message.Body = new TextPart("plain")
            {
                Text = $"Garage door open at {DateTime.Now}"
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
