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

        bool sqlOffline = true;
        bool sensorOffline = true;
        bool open = false;
        bool sent = false;

        private const int reedPinNum = 5;
        GpioController gpio;
        GpioPin reedPin;
        GpioPinValue reedValue;

        string sqlConn;

        DispatcherTimer timer = new DispatcherTimer();

        public MainPage()
        {
            this.InitializeComponent();

            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += TimerTick;

            SetupGPIOPins();
            SetupSqlConn();
            CheckDoor();
            UpdateBoard();

            timer.Start();
        }

        private void TimerTick(object sender, object e)
        {
            ticks++;
            txbTimer.Text = (tickInterval - ticks).ToString();
            //SendEmail();

            if (ticks >= tickInterval)
            {
                ticks = 0;

                if (sensorOffline)
                    SetupGPIOPins();

                CheckDoor();
                UpdateSQL();
                UpdateBoard();
            }
        }

        private void SetupGPIOPins()
        {
            #region
            // Latch HIGH value first. This ensures a default value when the pin is set as output
            //reedPin.Write(GpioPinValue.High);

            // Set the IO direction as input


            //probably a better way to run this program instead of running a timer non-stop:
            //event triggers when pin value changed
            //start timer, after so long send email
            //stop timer when pin changed again, send email saying its closed 
            //(uncomment below)


            #endregion

            try
            {
                gpio = GpioController.GetDefault();

                reedPin = gpio.OpenPin(reedPinNum);
                reedPin.SetDriveMode(GpioPinDriveMode.Input);

                reedValue = reedPin.Read();
                reedPin.ValueChanged += PinChange;
                sensorOffline = false;
            }
            catch (Exception ex)
            {
                sensorOffline = true;
                checks = 0;
                sent = false;
                open = false;
            }
        }

        private void PinChange(object sender, object e)
        {
            if (reedPin.Read() == GpioPinValue.High)
            {
                open = true;
            }
            else if (reedPin.Read() == GpioPinValue.Low)
            {
                open = false;
            }
        }

        private void SetupSqlConn()
        {
            try
            {
                sqlConn = @"Server=tcp:WAMPA,49172\SQLEXPRESS;Database=GarMonDB;Trusted_Connection=True;User Id=albinodyno;Password=thelivingshitouttame";

                SqlConnection connection = new SqlConnection(sqlConn);
                SqlCommand cmd = new SqlCommand("GMTestSetup", connection);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString());

                //connection.Open();
                int i = cmd.ExecuteNonQuery();
                connection.Close();

                sqlOffline = false;
            }
            catch (Exception ex)
            {
                sqlOffline = true;
            }
        }

        private void CheckDoor()
        {
            if (sensorOffline)
                return;

            try
            {
                if (open)
                    HandleOpen();
                else
                {
                    open = false;
                    checks = 0;
                    sent = false;
                }
            }
            catch (Exception ex)
            {
                return;
            }
            #region
            //Check if sensor senses door
            //https://tutorials-raspberrypi.com/raspberry-pi-ultrasonic-sensor-hc-sr04/
            //https://github.com/microsoft/Windows-universal-samples/blob/master/Samples/ProximitySensor/cs/Scenario1_DataEvents.xaml.cs

            //magnet sensors: https://tutorials-raspberrypi.com/raspberry-pi-door-window-sensor-with-reed-relais/

            //Reed Sensor map: When the switch block stand alone, there is continuity from "COM" to "NC". 
            //Introducing a magnetic field from the second block switches continuity from "COM" to "NO". 
            //Note that there are small triangle symbols on the switch to indicate the internal magnet location in the block. 
            //These triangles should be less than 5mm apart to activate the switch

            //reedValue = reedPin.Read();

            //if (reedValue == GpioPinValue.High)
            //{
            //    open = true;
            //    HandleOpen();
            //}
            //else
            //{
            //    open = false;
            //    checks = 0;
            //    sent = false;
            //}
            #endregion
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
            if (!sensorOffline && !open)
            {
                txbStatus.Text = "Status : Closed";
                txbStatus.Foreground = new SolidColorBrush(Colors.DarkCyan);
            }
            else if(!sensorOffline && open)
            {
                txbStatus.Text = "Status : OPEN";
                txbStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }
            else
            {
                txbStatus.Text = "Status : Unknown";
                txbStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }

            //Update Email Status
            if (!open && !sent)
                txbEStatus.Text = "";
            else if (open && !sent)
            {
                txbEStatus.Text = $"Not Sent: {checksToEmail - checks} remaining";
                txbEStatus.Foreground = new SolidColorBrush(Colors.DarkCyan);
            }
            else if (open && sent)
            {
                txbEStatus.Text = "Email : SENT";
                txbEStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }
            else if(!open && sent)
            {
                txbEStatus.Text = "";
            }

            //Update Pin Status
            if (sensorOffline)
            {
                txbSensorStatus.Text = "Offline";
                txbSensorStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }
            else
            {
                txbSensorStatus.Text = "Online";
                txbSensorStatus.Foreground = new SolidColorBrush(Colors.DarkCyan);
            }

            //Update SQL Status
            if (sqlOffline)
            {
                txbSqlStatus.Text = "Offline";
                txbSqlStatus.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }
            else
            {
                txbSqlStatus.Text = "Online" + DateTime.Now.ToString();
                txbSqlStatus.Foreground = new SolidColorBrush(Colors.DarkCyan);
            }

        }

        private void UpdateSQL()
        {
            if (!sqlOffline)
            {
                SqlConnection connection = new SqlConnection(sqlConn);
                SqlCommand cmd = new SqlCommand("CheckInsert", connection);
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
            else
                SetupSqlConn();
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
