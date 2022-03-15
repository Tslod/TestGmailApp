using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using static Google.Apis.Gmail.v1.GmailService;
using MimeKit;
using System.Xml.Serialization;
using DevExpress.XtraEditors;

namespace TestGmailApp
{
    public partial class Form1 : XtraForm
    {
        string[] Scopes = { Scope.GmailSend , Scope.GmailReadonly };
        string ApplicationName = "PrismaNET ERP";
        List<string> attachments = new List<string>();
        string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GmailTokenResponses");
        private MailSettings mailSettings = new MailSettings();

        public Form1()
        {
            InitializeComponent();

            //mailSettings.LoadFromXmlFile();

            if (!string.IsNullOrEmpty(mailSettings.Username)
                && Directory.Exists(Path.Combine(credPath, mailSettings.Username)))
            {
                credPath = Path.Combine(credPath, mailSettings.Username);
                sbGetGAccount.Text = "Αλλαγή Google Account";
                teFrom.Text = mailSettings.Username;
            }
        }

        #region HELPERS

        public GmailService GetService()
        {
            UserCredential credential = null;

            using (var stream =
                new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                try
                {
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true)).Result;
                }
                catch (AggregateException ex)
                {

                }
            }

            if (credential == null)
                return null;

            //θέλω και τα 2 scopes ΑΠΑΡΑΙΤΗΤΑ!!! Αν ο χρήστης δεν επιτρέψει ένα από αυτά του λέω αν θέλει να ξαναπροσπαθήσει!
            if (!credential.Token.Scope.Contains("https://www.googleapis.com/auth/gmail.readonly")
                || !credential.Token.Scope.Contains("https://www.googleapis.com/auth/gmail.send"))
            {
                ClearResponseToken(); // καθαρίζω το "σκουπίδι" response αρχείο

                if (XtraMessageBox.Show("Πρέπει να αποδεχτείτε όλες τις άδειες για να προχωρήσετε.Θέλετε να προσπαθήσετε ξανά;", "Προσοχή", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                    == DialogResult.Yes)
                    return GetService();
                else
                    return null;
            }

            GmailService service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            return service;
        }

        private string Base64UrlEncode(string input)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(inputBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private MimeMessage PrepareMimeMessage(string subject ,string body , List<string> attachments , List<string> recipients)
        {
            MimeMessage mimeMessage = null;

            MailMessage mail = new MailMessage();
            mail.Subject = subject;
            mail.Body = body;
            mail.From = new MailAddress(teFrom.Text);
            mail.IsBodyHtml = true;

            attachments.ForEach(att =>
            {
                mail.Attachments.Add(new Attachment(att));
            });

            recipients.ForEach(rec =>
            {
                mail.To.Add(new MailAddress(rec));
            });

            mimeMessage = MimeMessage.CreateFromMailMessage(mail);
            
            return mimeMessage;
        }

        private void ClearResponseToken()
        {
            DirectoryInfo di = new DirectoryInfo(credPath);

            foreach (FileInfo file in di.GetFiles())
                file.Delete();
        }

        private void MoveResponseTokenToEmailFolder(string emailString)
        {
            DirectoryInfo dirInfo = Directory.CreateDirectory(Path.Combine(credPath, emailString));

            Directory.GetFiles(credPath).ToList().ForEach(file =>
            {
                string destFile = Path.Combine(dirInfo.FullName, Path.GetFileName(file));

                if (!System.IO.File.Exists(destFile))
                    System.IO.File.Move(file, destFile);
                else
                {
                    System.IO.File.Copy(file, destFile, true);
                    System.IO.File.Delete(file);
                }
            });
        }

        #endregion

        private void btnBrowseFile_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog ofdAttachment = new OpenFileDialog())
                {
                    ofdAttachment.Multiselect = true;

                    if (ofdAttachment.ShowDialog() == DialogResult.OK)
{
                        foreach (string file in ofdAttachment.FileNames)
                            attachments.Add(file);

                        listBoxControl1.Items.AddRange(attachments.ToArray());
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }    

        private void sbGetGAccount_Click(object sender, EventArgs e)
        {
            credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GmailTokenResponses");

            GmailService GServices = GetService();

            if (GServices == null)
                return;

            var gmailProfile = GServices.Users.GetProfile("me").Execute();
            var userGmailEmail = gmailProfile.EmailAddress;
            teFrom.Text = mailSettings.Username = userGmailEmail;
            MoveResponseTokenToEmailFolder(userGmailEmail);
            //mailSettings.SaveToXmlFile();
            sbGetGAccount.Text = "Αλλαγή Google Account";
        }

        private void sbSendGMail_Click(object sender, EventArgs e)
        {
            credPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GmailTokenResponses"), mailSettings.Username);

            bool success = true;

            GmailService GServices = GetService();

            if (GServices == null)
                return;

            var gMailmessage = new Google.Apis.Gmail.v1.Data.Message();
            gMailmessage.Raw = Base64UrlEncode(PrepareMimeMessage(teSubject.Text, richEditControl1.HtmlText, attachments, new List<string> { teTo.Text }).ToString());

            try
            {
                GServices.Users.Messages.Send(gMailmessage, "me").Execute();
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show(ex.Message, "Σφάλμα", MessageBoxButtons.OK, MessageBoxIcon.Error);
                success = false;
            }

            if (success)
                XtraMessageBox.Show("Το mail στάλθηκε επιτυχώς !", "Πληροφορία", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void sbClearSelectedAttachments_Click(object sender, EventArgs e)
        {
            var attachmentsToExlude = listBoxControl1.SelectedItems.ToList();
            var newAttachments = attachments.Except(attachmentsToExlude).ToArray();
            attachments.Clear();
            listBoxControl1.Items.Clear();

            attachments = Array.ConvertAll(newAttachments, element => element.ToString()).ToList();
            listBoxControl1.Items.AddRange(attachments.ToArray());
        }
    }

    [XmlRoot("MailSettings")]
    public class MailSettings : SettingsFileBase
    {
        private string _username = string.Empty;

        [XmlElement("Username")]
        public string Username
        {
            get { return _username; }
            set { _username = value; }
        }

        public MailSettings()
        {
            Name = "MailSettingsXmlFile";
        }

        public override string GetFilePath(bool local)
        {
            string resval = (local ?
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) + string.Format(@"\{0}", Application.ProductName);

            return resval;
        }
    }
}

