using System;
using System.Drawing;
using System.Windows.Forms;

namespace Allumi.WindowsSensor
{
    public class InstallerOnboardingForm : Form
    {
        public bool PolicyAgreed { get; private set; } = false;
        private Button agreeButton;
        private Button launchButton;
        private CheckBox agreeCheckBox;
        private Label welcomeLabel;
        private TextBox policyTextBox;

        public InstallerOnboardingForm()
        {
            Text = "Welcome to Allumi Sensor - Privacy Notice";
            Width = 600;
            Height = 500;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            welcomeLabel = new Label
            {
                Text = "Welcome to Allumi Sensor! Please review what data we collect:",
                AutoSize = false,
                Width = 560,
                Height = 30,
                Top = 15,
                Left = 20,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };
            Controls.Add(welcomeLabel);

            policyTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Width = 560,
                Height = 280,
                Top = 50,
                Left = 20,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Text = @"WHAT DATA WE COLLECT:

• Active Window Titles: The titles of windows you have open (e.g., ""Document.docx - Microsoft Word"")
• Application Names: The programs you use (e.g., Chrome, VS Code, Excel)
• Window Activity Duration: How long each window is active
• Idle Time: Periods when you're not actively using your computer
• Device Information: Your device name and unique device ID
• Timestamps: When each activity occurs

WHY WE COLLECT THIS DATA:

This data helps you:
• Understand your productivity patterns
• Track time spent on different tasks
• Analyze your work habits
• Generate insights about your daily activities

HOW WE USE YOUR DATA:

• Data is synced to your Allumi account in real-time
• Data is stored securely on our servers
• Only you can see your activity data
• We do NOT collect keyboard input, mouse clicks, file contents, or passwords
• We do NOT share your data with third parties
• You can delete your data anytime from the dashboard

YOUR RIGHTS:

• You can stop tracking anytime by closing the app
• You can delete your device from your Allumi dashboard
• You can request deletion of all your data
• Contact us at support@allumi.ai for data requests

By clicking ""I Agree"", you consent to this data collection."
            };
            Controls.Add(policyTextBox);

            agreeCheckBox = new CheckBox
            {
                Text = "I agree and understand that my computer activity will be tracked and synced to Allumi",
                AutoSize = false,
                Width = 560,
                Height = 40,
                Top = 345,
                Left = 20
            };
            Controls.Add(agreeCheckBox);

            agreeButton = new Button
            {
                Text = "I Agree",
                Width = 120,
                Height = 35,
                Top = 400,
                Left = 20,
                Enabled = false
            };
            agreeButton.Click += (s, e) => {
                PolicyAgreed = true;
                agreeButton.Enabled = false;
                if (launchButton != null)
                    launchButton.Enabled = true;
            };
            Controls.Add(agreeButton);

            launchButton = new Button
            {
                Text = "Launch App",
                Width = 120,
                Height = 35,
                Top = 400,
                Left = 160,
                Enabled = false
            };
            launchButton.Click += (s, e) => {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(launchButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 120,
                Height = 35,
                Top = 400,
                Left = 300
            };
            cancelButton.Click += (s, e) => {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);

            agreeCheckBox.CheckedChanged += (s, e) => {
                agreeButton.Enabled = agreeCheckBox.Checked;
            };
        }
    }
}
