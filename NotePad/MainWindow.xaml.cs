using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NotePad
{
    public partial class MainWindow : Window
    {
        private Dictionary<TabItem, DocumentInfo> documents;
        private DispatcherTimer autoSaveTimer;
        private bool isAutoSaveEnabled = true;
        private FindDialog findDialog;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
            
        }

        private void InitializeApplication()
        {
            documents = new Dictionary<TabItem, DocumentInfo>();
            
            // Inicializar o primeiro documento
            var firstTab = tabControl.Items[0] as TabItem;
            documents[firstTab] = new DocumentInfo();
            
            // Configurar timer de salvamento automático
            SetupAutoSaveTimer();
            
            // Atualizar status inicial
            UpdateStatusBar();
        }

        private void SetupAutoSaveTimer()
        {
            autoSaveTimer = new DispatcherTimer();
            autoSaveTimer.Interval = TimeSpan.FromMinutes(1);
            autoSaveTimer.Tick += AutoSaveTimer_Tick;
            autoSaveTimer.Start();
        }

        #region Comandos do Menu Arquivo

        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                Title = "Abrir Arquivo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openFileDialog.FileName);
                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    
                    CreateNewTab(fileName, content, openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao abrir arquivo: {ex.Message}", "Erro", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveCurrentDocument();
        }

        private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveCurrentDocumentAs();
        }

        #endregion

        #region Comandos do Menu Editar

        private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GetCurrentTextBox()?.Cut();
        }

        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GetCurrentTextBox()?.Copy();
        }

        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GetCurrentTextBox()?.Paste();
        }

        private void SelectAllCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GetCurrentTextBox()?.SelectAll();
        }

        private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (findDialog == null || !findDialog.IsVisible)
            {
                findDialog = new FindDialog(this);
                findDialog.Show();
            }
            else
            {
                findDialog.Activate();
            }
        }

        #endregion

        #region Gerenciamento de Abas

        private void CreateNewTab(string title = "Novo Documento", string content = "", string filePath = null)
        {
            TabItem newTab = new TabItem();
            
            // Criar botão de fechar na aba
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
            TextBlock titleBlock = new TextBlock { Text = title, Margin = new Thickness(0, 0, 5, 0) };
            Button closeButton = new Button 
            { 
                Content = "×", 
                Width = 16, 
                Height = 16,
                Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold
            };
            
            closeButton.Click += (s, e) => CloseTab(newTab);
            
            header.Children.Add(titleBlock);
            header.Children.Add(closeButton);
            newTab.Header = header;

            // Criar conteúdo da aba
            ScrollViewer scrollViewer = new ScrollViewer();
            TextBox textBox = new TextBox
            {
                Text = content,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                Margin = new Thickness(5)
            };

            textBox.TextChanged += TextEditor_TextChanged;
            textBox.SelectionChanged += TextEditor_SelectionChanged;

            scrollViewer.Content = textBox;
            newTab.Content = scrollViewer;

            // Adicionar aba ao controle
            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;

            // Registrar documento
            documents[newTab] = new DocumentInfo
            {
                FilePath = filePath,
                OriginalTitle = title,
                IsModified = false
            };

            // Aplicar realce de sintaxe se necessário
            ApplySyntaxHighlighting(textBox, filePath);
        }

        private void CloseTab(TabItem tab)
        {
            if (tabControl.Items.Count <= 1)
            {
                // Não permitir fechar a última aba
                return;
            }

            var doc = documents[tab];
            if (doc.IsModified)
            {
                var result = MessageBox.Show(
                    $"O documento '{GetTabTitle(tab)}' foi modificado. Deseja salvar as alterações?",
                    "Salvar alterações?",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (!SaveDocument(tab))
                        return; // Cancelar fechamento se não conseguir salvar
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return; // Cancelar fechamento
                }
            }

            documents.Remove(tab);
            tabControl.Items.Remove(tab);
        }

        #endregion

        #region Funcionalidades de Salvamento

        private bool SaveCurrentDocument()
        {
            var currentTab = tabControl.SelectedItem as TabItem;
            return SaveDocument(currentTab);
        }

        private bool SaveCurrentDocumentAs()
        {
            var currentTab = tabControl.SelectedItem as TabItem;
            return SaveDocumentAs(currentTab);
        }

        private bool SaveDocument(TabItem tab)
        {
            if (tab == null) return false;

            var doc = documents[tab];
            if (string.IsNullOrEmpty(doc.FilePath))
            {
                return SaveDocumentAs(tab);
            }

            try
            {
                var textBox = GetTextBoxFromTab(tab);
                File.WriteAllText(doc.FilePath, textBox.Text);
                doc.IsModified = false;
                UpdateTabTitle(tab);
                statusText.Text = $"Arquivo salvo: {doc.FilePath}";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar arquivo: {ex.Message}", "Erro",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool SaveDocumentAs(TabItem tab)
        {
            if (tab == null) return false;

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Arquivos de Texto (*.txt)|*.txt|Todos os Arquivos (*.*)|*.*",
                Title = "Salvar Como"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var textBox = GetTextBoxFromTab(tab);
                    File.WriteAllText(saveFileDialog.FileName, textBox.Text);
                    
                    var doc = documents[tab];
                    doc.FilePath = saveFileDialog.FileName;
                    doc.OriginalTitle = Path.GetFileName(saveFileDialog.FileName);
                    doc.IsModified = false;
                    
                    UpdateTabTitle(tab);
                    ApplySyntaxHighlighting(textBox, doc.FilePath);
                    statusText.Text = $"Arquivo salvo: {doc.FilePath}";
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao salvar arquivo: {ex.Message}", "Erro",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return false;
        }

        #endregion

        #region Eventos de Interface

        private void TextEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            var currentTab = GetTabFromTextBox(textBox);
            
            if (currentTab != null && documents.ContainsKey(currentTab))
            {
                documents[currentTab].IsModified = true;
                UpdateTabTitle(currentTab);
            }

            UpdateStatusBar();
        }

        private void TextEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateCursorPosition();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatusBar();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Verificar documentos modificados antes de fechar
            foreach (var doc in documents.Values)
            {
                if (doc.IsModified)
                {
                    var result = MessageBox.Show(
                        "Existem documentos não salvos. Deseja realmente sair?",
                        "Confirmar saída",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.No)
                        return;
                    break;
                }
            }
            Application.Current.Shutdown();
        }

        private void StatusBarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            statusBar.Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AutoSaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            isAutoSaveEnabled = menuItem.IsChecked;
        }

        #endregion

        #region Métodos Auxiliares

        private TextBox GetCurrentTextBox()
        {
            var currentTab = tabControl.SelectedItem as TabItem;
            return GetTextBoxFromTab(currentTab);
        }

        private TextBox GetTextBoxFromTab(TabItem tab)
        {
            if (tab?.Content is ScrollViewer scrollViewer)
            {
                return scrollViewer.Content as TextBox;
            }
            return null;
        }

        private TabItem GetTabFromTextBox(TextBox textBox)
        {
            foreach (TabItem tab in tabControl.Items)
            {
                if (GetTextBoxFromTab(tab) == textBox)
                    return tab;
            }
            return null;
        }

        private string GetTabTitle(TabItem tab)
        {
            if (tab.Header is StackPanel panel && panel.Children[0] is TextBlock textBlock)
            {
                return textBlock.Text;
            }
            return "Documento";
        }

        private void UpdateTabTitle(TabItem tab)
        {
            if (tab.Header is StackPanel panel && panel.Children[0] is TextBlock textBlock)
            {
                var doc = documents[tab];
                string title = doc.OriginalTitle;
                if (doc.IsModified)
                    title += " *";
                textBlock.Text = title;
            }
        }

        private void UpdateStatusBar()
        {
            var textBox = GetCurrentTextBox();
            if (textBox != null)
            {
                charCountText.Text = $"Caracteres: {textBox.Text.Length}";
                wordCountText.Text = $"Palavras: {CountWords(textBox.Text)}";
            }
        }

        private void UpdateCursorPosition()
        {
            var textBox = GetCurrentTextBox();
            if (textBox != null)
            {
                int line = textBox.GetLineIndexFromCharacterIndex(textBox.CaretIndex) + 1;
                int column = textBox.CaretIndex - textBox.GetCharacterIndexFromLineIndex(line - 1) + 1;
                cursorPositionText.Text = $"Lin: {line}, Col: {column}";
            }
        }

        private int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;
            
            return Regex.Matches(text, @"\b\w+\b").Count;
        }

        private void ApplySyntaxHighlighting(TextBox textBox, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            string extension = Path.GetExtension(filePath).ToLower();
            
            // Simples mudança de cor baseada na extensão
            switch (extension)
            {
                case ".xml":
                case ".xaml":
                    textBox.Foreground = Brushes.DarkBlue;
                    break;
                case ".json":
                    textBox.Foreground = Brushes.DarkGreen;
                    break;
                case ".cs":
                    textBox.Foreground = Brushes.DarkMagenta;
                    break;
                default:
                    textBox.Foreground = Brushes.Black;
                    break;
            }
        }

        #endregion

        #region Salvamento Automático

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            if (!isAutoSaveEnabled) return;

            foreach (var kvp in documents)
            {
                var tab = kvp.Key;
                var doc = kvp.Value;
                
                if (doc.IsModified && !string.IsNullOrEmpty(doc.FilePath))
                {
                    try
                    {
                        var textBox = GetTextBoxFromTab(tab);
                        File.WriteAllText(doc.FilePath, textBox.Text);
                        doc.IsModified = false;
                        UpdateTabTitle(tab);
                    }
                    catch
                    {
                        // Ignorar erros de salvamento automático
                    }
                }
            }
        }

        #endregion

        #region Funcionalidade de Busca

        public void FindText(string searchText, bool matchCase)
        {
            var textBox = GetCurrentTextBox();
            if (textBox == null || string.IsNullOrEmpty(searchText)) return;

            StringComparison comparison = matchCase ? 
                StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int startIndex = textBox.SelectionStart + textBox.SelectionLength;
            int foundIndex = textBox.Text.IndexOf(searchText, startIndex, comparison);

            if (foundIndex == -1)
            {
                // Buscar do início
                foundIndex = textBox.Text.IndexOf(searchText, 0, comparison);
            }

            if (foundIndex != -1)
            {
                textBox.Select(foundIndex, searchText.Length);
                textBox.ScrollToLine(textBox.GetLineIndexFromCharacterIndex(foundIndex));
                textBox.Focus();
            }
            else
            {
                MessageBox.Show("Texto não encontrado.", "Busca", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion
    }

    public class DocumentInfo
    {
        public string FilePath { get; set; }
        public string OriginalTitle { get; set; } = "Novo Documento";
        public bool IsModified { get; set; } = false;
    }
}