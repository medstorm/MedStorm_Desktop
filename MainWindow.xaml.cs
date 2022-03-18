using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NameCrypto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MedStormCrypto m_medStormCrypto;
        public MainWindow()
        {
            InitializeComponent();
            m_medStormCrypto = new MedStormCrypto();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }
        private void CryptButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] cryptedBytes = m_medStormCrypto.EncryptStringToBytes_Aes(plainTextBox.Text);
            cryptedTextBox.Text = Convert.ToBase64String(cryptedBytes);
            plainTextBox.Text = "";
        }

        private void DeCryptButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] hexCryptedBytes= Convert.FromBase64String(cryptedTextBox.Text);
            string? plainText = m_medStormCrypto.DecryptStringFromBytes_Aes(hexCryptedBytes);
            plainTextBox.Text = plainText;
            cryptedTextBox.Text = "";
        }

    }
}
