using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Net.Mail;
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
using System.Data.SqlClient;

namespace GarMon.App
{
    public sealed partial class MainPage : Page
    {
        int tickInterval = 30;
        int checksToEmail = 3;

        int ticks = 0;
        int checks = 0;

        bool sqlOffline;
        bool sensorOffline;
        bool open = false;
        bool sent = false;

        GpioController gpio;
        GpioPin reedPin;

        string sqlConn;

        DispatcherTimer timer = new DispatcherTimer();

        public MainPage()
        {
            this.InitializeComponent();

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerTick;
            timer.Start();

            SetupGPIOPins();
            SetupSqlConn();
            UpdateBoard();
        }

        private void TimerTick(object sender, object e)
        {
            ticks++;
            txbTimer.Text = (tickInterval - ticks).ToString();
            //SendEmail();

            if (ticks >= tickInterval)
            {
                ticks = 0;
                CheckDoor();
                UpdateBoard();
                UpdateSQL();

                if (sensorOffline)
                    SetupGPIOPins();

                if (sqlOffline)
                    SetupSqlConn();
            }
        }

        private void SetupGPIOPins()
        {
            gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                sensorOffline = true;
                return; // GPIO not available on this system
            }


            using (reedPin = gpio.OpenPin(5))
            {
                // Latch HIGH value first. This ensures a default value when the pin is set as output
                //reedPin.Write(GpioPinValue.High);

                // Set the IO direction as input
                reedPin.SetDriveMode(GpioPinDriveMode.Input);

                //probably a better way to run this program instead of running a timer non-stop:
                //event triggers when pin value changed
                //start timer, after so long send email
                //stop timer when pin changed again, send email saying its closed 
                //(uncomment below)

                //reedPin.ValueChanged += PinChange;
                sensorOffline = false;
            }
        }

        private void SetupSqlConn()
        {
            sqlConn = @"Data Source=YourServerName\SQLEXPRESS;Initial Catalog=NORTHWIND;Integrated Security=SSPI";
            //somehow test connection?
        }

        private void PinChange(object sender, object e)
        {
            if (reedPin.Read() == GpioPinValue.High)
            {
                open = true;
            }
        }

        private void CheckDoor()
        {
            //Check if sensor senses door
            //https://tutorials-raspberrypi.com/raspberry-pi-ultrasonic-sensor-hc-sr04/
            //https://github.com/microsoft/Windows-universal-samples/blob/master/Samples/ProximitySensor/cs/Scenario1_DataEvents.xaml.cs

            //magnet sensors: https://tutorials-raspberrypi.com/raspberry-pi-door-window-sensor-with-reed-relais/

            //Reed Sensor map: When the switch block stand alone, there is continuity from "COM" to "NC". 
            //Introducing a magnetic field from the second block switches continuity from "COM" to "NO". 
            //Note that there are small triangle symbols on the switch to indicate the internal magnet location in the block. 
            //These triangles should be less than 5mm apart to activate the switch

            if (reedPin != null && reedPin.Read() == GpioPinValue.High)
            {
                open = true;
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
            UpdateSQL();

            if (checks > checksToEmail && !sent)
            {
                SendEmail();
                sent = true;
            }
        }

        private void UpdateBoard()
        {
            //Update Checks
            txbChecks.Text = "Checks : " + checks.ToString();

            //Update Door Status
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

            //Update Email Status
            if (!open && !sent)
                txbEStatus.Text = "";
            else if (open && !sent)
                txbEStatus.Text = $"Not Sent: {checksToEmail - checks} left";
            else
                txbEStatus.Text = "Email :::SENT:::";

            //Update Pin Status
            if (reedPin == null)
                txbSensorStatus.Text = "Offline";
            else
                txbSensorStatus.Text = "Online";

            //Update SQL Status
            if(!sqlOffline)
            {
                txbSqlStatus.Text = "Online";
            }
            else
            {
                txbSqlStatus.Text = "Offline";
            }

        }

        private void UpdateSQL()
        {
            if (!sqlOffline)
            {
                SqlConnection connection = new SqlConnection(sqlConn);
                SqlCommand cmd = new SqlCommand("sp_insert", connection);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString());

                connection.Open();
                int i = cmd.ExecuteNonQuery();
                connection.Close();

                if (i != 0)
                    txbLastSql.Text = "Successful " + DateTime.Now.ToString();
                else
                    txbLastSql.Text = "Unsuccessful " + DateTime.Now.ToString();
            }
        }

        private void SendEmail()
        {
            var message = new MimeMessage();

            message.Sender = new MailboxAddress("Jared Buchanan", "jbbuchanan266@gmail.com");
            message.To.Add(new MailboxAddress("Jared Buchanan", "jbbuchanan266@gmail.com"));
            message.Subject = $"Garage Door: {DateTime.Now}";

            message.Body = new TextPart("plain")
            {
                Text = $"Garage door open at {DateTime.Now}"
            };

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate("jbbuchanan266@gmail.com", "Motorola266!");
                client.AuthenticationMechanisms.Remove("XOAUTH2");                

                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
