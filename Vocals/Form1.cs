﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Vocals.InternalClasses;
using System.Xml.Serialization;


//TODO Corriger 9/PGUP
//TODO : Retour mp3
//TODO : Resize
//TODO : Add random phrases
//TODO : Add listen to worda

namespace Vocals {
    public partial class Form1 : Form {

        protected delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        protected static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        protected static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        protected static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")]
        protected static extern bool IsWindowVisible(IntPtr hWnd);

        List<string> myWindows;
        List<Profile> profileList;
        IntPtr winPointer;

        SpeechRecognitionEngine speechEngine;

        Options currentOptions;

        private GlobalHotkey ghk;

        bool listening = false;

        string xmlProfilesFileName = "vocals_profiles.xml";
        string version;

        public Form1() {
            InitializeComponent();
            initializeSpeechEngine();

            myWindows = new List<string>();
            refreshProcessList();

            fetchProfiles();

            ghk = new GlobalHotkey(0x0004, Keys.None, this);

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyName assemblyName = assembly.GetName();
            this.version = assemblyName.Version.ToString();
            //this.Text += " version : " + version;

            if (comboBox_profiles.Items.Count > 0)
            {
                this.Text = "Vocals profile: " + comboBox_profiles.Items[0].ToString();
            }
            
            currentOptions = new Options();
            refreshSettings();

        }

        public void handleHookedKeypress() {
            if (listening == false) {
                if (speechEngine.Grammars.Count > 0) {
                    speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                    SpeechSynthesizer synth = new SpeechSynthesizer();
                    synth.SpeakAsync(currentOptions.answer);
                    listening = !listening;
                }

            }
            else {
                if (speechEngine.Grammars.Count > 0) {
                    speechEngine.RecognizeAsyncCancel();
                    SpeechSynthesizer synth = new SpeechSynthesizer();
                    synth.SpeakAsync(currentOptions.answer);
                    listening = !listening;
                }
            }
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x0312) {
                handleHookedKeypress();
            }
            base.WndProc(ref m);
        }

        public void refreshProcessList() {
            EnumWindows(new EnumWindowsProc(EnumTheWindows), IntPtr.Zero);
            comboBox_processes.DataSource = null;
            comboBox_processes.DataSource = myWindows;

        }

        void fetchProfiles() {
            string dir = @"";
            string xmlSerializationFile = Path.Combine(dir, xmlProfilesFileName);
            try {
                Stream xmlStream = File.Open(xmlSerializationFile, FileMode.Open);
                XmlSerializer reader = new XmlSerializer(typeof(List<Profile>));
                profileList = (List<Profile>)reader.Deserialize(xmlStream);
                xmlStream.Close();
            }
            catch {
                profileList = new List<Profile>();
                //MessageBox.Show("Can't load profiles file: " + xmlProfilesFileName, "No profiles?");
            }
            comboBox_profiles.DataSource = profileList;
        }

        private bool saveProfiles()
        {
            string dir = @"";
            string xmlSerializationFile = Path.Combine(dir, this.xmlProfilesFileName);

            try
            {
                Stream xmlStream = File.Open(xmlSerializationFile, FileMode.Create);
                System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(List<Profile>));
                writer.Serialize(xmlStream, profileList);
                xmlStream.Close();
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        private static void Get45or451FromRegistry() {
            using (RegistryKey ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
               RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\")) {
                int releaseKey = (int)ndpKey.GetValue("Release");
                {
                    if (releaseKey == 378389)

                        Console.WriteLine("The .NET Framework version 4.5 is installed");

                    if (releaseKey == 378758)

                        Console.WriteLine("The .NET Framework version 4.5.1  is installed");

                }
            }
        }

        private void Form1_Load(object sender, EventArgs e) {
            Get45or451FromRegistry();

        }

        void initializeSpeechEngine() {
            richTextBox1.AppendText("Starting Speech Recognition Engine \n");
            RecognizerInfo info = null;
            foreach (RecognizerInfo ri in SpeechRecognitionEngine.InstalledRecognizers()) {
                if (ri.Culture.Equals(System.Globalization.CultureInfo.CurrentCulture)) {
                    richTextBox1.AppendText("Setting VR engine language to " + ri.Culture.DisplayName + "\n");
                    info = ri;
                    break;
                }
            }

            if (info == null && SpeechRecognitionEngine.InstalledRecognizers().Count != 0) {
                RecognizerInfo ri = SpeechRecognitionEngine.InstalledRecognizers()[0];
                richTextBox1.AppendText("Setting VR engine language to " + ri.Culture.DisplayName + "\n");
                info = ri;
            }

            if (info == null) {
                richTextBox1.AppendText("Could not find any installed recognizers\n");
                richTextBox1.AppendText("Trying to find a fix right now for this specific error\n");
                return;
            }
            speechEngine = new SpeechRecognitionEngine(info);
            speechEngine.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(sr_speechRecognized);
            speechEngine.AudioLevelUpdated += new EventHandler<AudioLevelUpdatedEventArgs>(sr_audioLevelUpdated);

            try {
                speechEngine.SetInputToDefaultAudioDevice();
            }
            catch (InvalidOperationException ioe) {
                richTextBox1.AppendText("No microphone found\n");
            }

            speechEngine.MaxAlternates = 3;


        }

        void sr_audioLevelUpdated(object sender, AudioLevelUpdatedEventArgs e) {
            if (speechEngine != null) {
                int val = (int)(10*Math.Sqrt(e.AudioLevel));
                this.progressBar1.Value = val;
            }
        }



        void sr_speechRecognized(object sender, SpeechRecognizedEventArgs e) {

            richTextBox1.AppendText("Command recognized \"" + e.Result.Text + "\" with confidence of : " + Math.Round(e.Result.Confidence, 2) + "\n");

            Profile p = (Profile)comboBox_profiles.SelectedItem;

            if (p != null) {
                foreach (Command c in p.commandList) {
                    string[] multiCommands = c.commandString.Split(';');
                    foreach (string s in multiCommands) {
                        string correctedWord = s.Trim().ToLower();
                        if (correctedWord.Equals(e.Result.Text)) {
                            c.perform(winPointer);
                            break;
                        }
                    }
                }
            }

        }



        protected bool EnumTheWindows(IntPtr hWnd, IntPtr lParam) {
            int size = GetWindowTextLength(hWnd);
            if (size++ > 0 && IsWindowVisible(hWnd)) {
                StringBuilder sb = new StringBuilder(size);
                GetWindowText(hWnd, sb, size);
                myWindows.Add(sb.ToString());
            }
            return true;
        }


        private void textBox1_TextChanged(object sender, EventArgs e) {

        }

        void createNewProfile() {
            FormNewProfile formNewProfile = new FormNewProfile();
            formNewProfile.ShowDialog();
            string profileName = formNewProfile.profileName;
            if (profileName != "") {
                Profile p = new Profile(profileName);
                profileList.Add(p);
                comboBox_profiles.DataSource = null;
                comboBox_profiles.DataSource = profileList;
                comboBox_profiles.SelectedItem = p;
            }
        }

        private void btn_add_profile_Click(object sender, EventArgs e) {
            createNewProfile();
        }

        private void comboBox_profiles_SelectedIndexChanged(object sender, EventArgs e) {
            if (speechEngine != null) {
                speechEngine.RecognizeAsyncCancel();
                listening = false;
            }

            Profile p = (Profile)comboBox_profiles.SelectedItem;
            if (p != null) {

                this.Text = "Vocals profile: " + p.name;

                refreshProfile(p);

                listBox1.DataSource = null;
                listBox1.DataSource = p.commandList;

                if (speechEngine.Grammars.Count != 0) {
                    speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                    listening = true;
                }
            }
        }

        void refreshProfile(Profile p) {
            if (p.commandList.Count != 0) {
                Choices myWordChoices = new Choices();

                foreach (Command c in p.commandList)
                {
                    string[] commandList = c.commandString.Split(';');
                    foreach (string s in commandList)
                    {
                        string correctedWord;
                        correctedWord = s.Trim().ToLower();
                        if (correctedWord != null && correctedWord != "")
                        {
                            myWordChoices.Add(correctedWord);
                        }
                    }
                }

                //if(myWordChoices.ToGrammarBuilder)
                GrammarBuilder builder = myWordChoices.ToGrammarBuilder();
                //builder.Append(myWordChoices);
                Grammar mygram = new Grammar(builder);


                speechEngine.UnloadAllGrammars();
                //speechEngine.LoadGrammar(mygram);
                speechEngine.LoadGrammarAsync(mygram);

            }
            else {
                speechEngine.UnloadAllGrammars();
            }

        }

        private void btn_add_cmd_Click(object sender, EventArgs e) {
            try {
                if (speechEngine != null) {
                    speechEngine.RecognizeAsyncCancel();
                    listening = false;

                    FormCommand formCommand = new FormCommand();
                    formCommand.Text = "New Command";
                    formCommand.ShowDialog();

                    Profile p = (Profile)comboBox_profiles.SelectedItem;

                    if (p != null) {
                        if (formCommand.commandString != null && formCommand.commandString != "" && formCommand.actionList.Count != 0) {
                            Command c;
                            c = new Command(formCommand.commandString, formCommand.actionList, formCommand.answering, formCommand.answeringString, formCommand.answeringSound, formCommand.answeringSoundPath);
                            p.addCommand(c);
                            listBox1.DataSource = null;
                            listBox1.DataSource = p.commandList;
                        }
                        refreshProfile(p);
                    }

                    if (speechEngine.Grammars.Count != 0) {
                        speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                        listening = true;
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private void comboBox_processes_SelectedIndexChanged(object sender, EventArgs e) {
            Process[] pTab = Process.GetProcesses();
            for (int i = 0; i < pTab.Length; i++) {
                if (pTab[i] != null && comboBox_processes.SelectedItem != null) {
                    if (pTab[i].MainWindowTitle.Equals(comboBox_processes.SelectedItem.ToString())) {
                        winPointer = pTab[i].MainWindowHandle;
                    }
                }
            }
        }

        private void label1_Click(object sender, EventArgs e) {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e) {

        }

        private void btn_del_profile_Click(object sender, EventArgs e) {
            Profile p = (Profile)(comboBox_profiles.SelectedItem);
            profileList.Remove(p);
            comboBox_profiles.DataSource = null;
            comboBox_profiles.DataSource = profileList;

            if (profileList.Count == 0) {
                listBox1.DataSource = null;
            }
            else {
                comboBox_profiles.SelectedItem = profileList[0];
                refreshProfile((Profile)comboBox_profiles.SelectedItem);
            }
        }



        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            
            if (saveProfiles())
            {
                speechEngine.AudioLevelUpdated -= new EventHandler<AudioLevelUpdatedEventArgs>(sr_audioLevelUpdated);
                speechEngine.SpeechRecognized -= new EventHandler<SpeechRecognizedEventArgs>(sr_speechRecognized);
            }
            else
            {
                DialogResult res = MessageBox.Show("The file " + xmlProfilesFileName + " is being used by another process.\n Exit without saving?", "Can't save profiles", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (res == DialogResult.No)
                {
                    e.Cancel = true;
                }
                
            }
            

        }


        private void btn_del_cmd_Click(object sender, EventArgs e) {
            Profile p = (Profile)(comboBox_profiles.SelectedItem);
            if (p != null) {
                Command c = (Command)listBox1.SelectedItem;
                if (c != null) {
                    if (speechEngine != null) {
                        speechEngine.RecognizeAsyncCancel();
                        listening = false;
                        p.commandList.Remove(c);
                        listBox1.DataSource = null;
                        listBox1.DataSource = p.commandList;

                        refreshProfile(p);

                        if (speechEngine.Grammars.Count != 0) {
                            speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                            listening = true;
                        }
                    }

                }
            }
        }

        private void button5_Click(object sender, EventArgs e) {
            myWindows.Clear();
        }

        private void groupBox2_Enter(object sender, EventArgs e) {

        }

        private void groupBox4_Enter(object sender, EventArgs e) {

        }

        private void btn_edit_cmd_Click(object sender, EventArgs e) {
            try {
                if (speechEngine != null) {
                    speechEngine.RecognizeAsyncCancel();
                    listening = false;


                    Command c = (Command)listBox1.SelectedItem;
                    if (c != null) {
                        FormCommand formCommand = new FormCommand(c);
                        formCommand.Text = "Edit Command";
                        formCommand.ShowDialog();

                        Profile p = (Profile)comboBox_profiles.SelectedItem;


                        if (p != null) {
                            if (formCommand.commandString != "" && formCommand.actionList.Count != 0) {

                                c.commandString = formCommand.commandString;
                                c.actionList = formCommand.actionList;
                                c.answering = formCommand.answering;
                                c.answeringString = formCommand.answeringString;
                                c.answeringSound = formCommand.answeringSound;
                                c.answeringSoundPath = formCommand.answeringSoundPath;

                                if (c.answeringSoundPath == null) {
                                    c.answeringSoundPath = "";
                                }
                                if (c.answeringString == null) {
                                    c.answeringString = "";
                                }
                               

                                listBox1.DataSource = null;
                                listBox1.DataSource = p.commandList;
                            }
                            refreshProfile(p);
                        }

                        if (speechEngine.Grammars.Count != 0) {
                            speechEngine.RecognizeAsync(RecognizeMode.Multiple);
                            listening = true;
                        }
                    }

                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
            }

        }

        private void groupBox3_Enter(object sender, EventArgs e) {

        }



        private void optionsToolStripMenuItem_Click(object sender, EventArgs e) {
            FormOptions formOptions = new FormOptions();
            formOptions.ShowDialog();

            currentOptions = formOptions.opt;
            refreshSettings();
        }

        private void refreshSettings() {
            applyModificationToGlobalHotKey();
            applyToggleListening();
            applyRecognitionSensibility();
            currentOptions.save();
        }

        private void applyModificationToGlobalHotKey() {
            if(currentOptions.key == Keys.Shift ||
                currentOptions.key == Keys.ShiftKey ||
                currentOptions.key == Keys.LShiftKey ||
                currentOptions.key == Keys.RShiftKey) {
                    ghk.modifyKey(0x0004, Keys.None);
            }
            else if(currentOptions.key == Keys.Control ||
                currentOptions.key == Keys.ControlKey ||
                currentOptions.key == Keys.LControlKey ||
                currentOptions.key == Keys.RControlKey) {
                ghk.modifyKey(0x0002,Keys.None);
                    
            }
            else if (currentOptions.key == Keys.Alt) {
                ghk.modifyKey(0x0002, Keys.None);
            }
            else {
                ghk.modifyKey(0x0000, currentOptions.key);
            }
        }

        private void applyToggleListening() {
            if (currentOptions.toggleListening) {
                try {
                    ghk.register();
                }
                catch {
                    Console.WriteLine("Couldn't register key properly");
                }
            }
            else {
                try {
                    ghk.unregister();
                }
                catch {
                    Console.WriteLine("Couldn't unregister key properly");
                }

            }
        }

        private void applyRecognitionSensibility() {
            if (speechEngine != null) {
                speechEngine.UpdateRecognizerSetting("CFGConfidenceRejectionThreshold", currentOptions.threshold );
            }
            
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {

        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) {

        }

        private void btn_refreshProcesses_Click(object sender, EventArgs e) {
            myWindows.Clear();
            refreshProcessList();
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void saveProfilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveProfiles())
            {
                richTextBox1.AppendText("Saved profiles successfully to " + xmlProfilesFileName);
            }
            else
            {
                richTextBox1.AppendText("Error: Can't save profiles to " + xmlProfilesFileName);
            }

        }





    }
}
