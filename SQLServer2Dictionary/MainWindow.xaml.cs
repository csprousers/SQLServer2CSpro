using Microsoft.Win32;
using SqlConnectionDialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
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

namespace SQLServer2Dictionary
{
    public class RecordSpec
    {
        public string Name { get; set; }
        public char Type { get; set; }
        public DatabaseTable DatabaseTable { get; set; }
    }

    public class LevelSpec
    {
        public int Number { get; set; }
        public String Name { get; set; }
        public ObservableCollection<DatabaseColumn> IdItems { get; set; }
        public ObservableCollection<RecordSpec> Records { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string connectionString;

        public ObservableCollection<DatabaseTable> DatabaseTables { get; set; }
        public ObservableCollection<LevelSpec> LevelSpecs { get; set; }

        public string DictionaryLabel { get; set; }

        public string DictionaryPath { get; set; }

        public string DatabaseName { get; set; }

        public MainWindow(string connectionString)
        {
            this.connectionString = connectionString;

            InitializeComponent();
            DataContext = this;
            LevelSpecs = new ObservableCollection<LevelSpec>();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    DatabaseName = connection.Database;

                    DictionaryLabel = connection.Database;
                    NotifyPropertyChanged("DictionaryLabel");
                    DictionaryPath = connection.Database + ".dcf";
                    NotifyPropertyChanged("DictionaryPath");

                    DatabaseTables = GetTables(connection);
                    NotifyPropertyChanged("DatabaseTables");

                    LevelSpecs.Add(
                        new LevelSpec
                        {
                            Number = 1,
                            Name = connection.Database + " questionnaire",
                            IdItems = new ObservableCollection<DatabaseColumn>(),
                            Records = new ObservableCollection<RecordSpec>()
                        });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Error: " + ex.Message);
                Close();
            }
            Mouse.OverrideCursor = oldCursor;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private static ObservableCollection<DatabaseTable> GetTables(SqlConnection connection)
        {
            DataTable schema = connection.GetSchema("Tables");
            List<DatabaseTable> tables = new List<DatabaseTable>();
            foreach (DataRow row in schema.Rows)
            {
                var table = new DatabaseTable
                {
                    Schema = row[1].ToString(),
                    Name = row[2].ToString()
                };

                table.Columns = GetTableColumns(table.FullName, connection);
                tables.Add(table);
            }
            return new ObservableCollection<DatabaseTable>(tables.OrderBy(t => t.Schema).ThenBy(t => t.Name));
        }

        private static List<DatabaseColumn> GetTableColumns(string table, SqlConnection connection)
        {
            var columns = new List<DatabaseColumn>();

            using (var cmd = new SqlCommand("SELECT * FROM " + table, connection))
            {
                using (var reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    var schemaTable = reader.GetSchemaTable();

                    foreach (DataRow field in schemaTable.Rows)
                    {
                        columns.Add(new DatabaseColumn
                        {
                            Name = (string)field["ColumnName"],
                            Type = (Type)field["DataType"],
                            Length = (int)field["ColumnSize"]
                        });
                    }
                }
            }

            return columns;
        }

        private void treeViewTables_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                TreeView tv = sender as TreeView;
                if (tv != null && tv.SelectedItem != null)
                {
                    DragDrop.DoDragDrop(tv,
                                tv.SelectedItem,
                                 DragDropEffects.Copy);
                }
            }
        }

        private void listBoxIdItems_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(typeof(DatabaseColumn)))
            {
                DatabaseColumn col = e.Data.GetData(typeof(DatabaseColumn)) as DatabaseColumn;
                var frameworkElement = sender as FrameworkElement;
                if (frameworkElement != null)
                {
                    var levelSpec = frameworkElement.DataContext as LevelSpec;
                    if (levelSpec.IdItems.FirstOrDefault(i => i == col) == null)
                        e.Effects = DragDropEffects.Copy;
                }
            }
        }

        private void listBoxIdItems_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(DatabaseColumn)))
            {
                DatabaseColumn col = e.Data.GetData(typeof(DatabaseColumn)) as DatabaseColumn;
                var frameworkElement = sender as FrameworkElement;
                if (frameworkElement != null)
                {
                    var levelSpec = frameworkElement.DataContext as LevelSpec;
                    if (levelSpec.IdItems.FirstOrDefault(i => i == col) == null)
                        levelSpec.IdItems.Add(col);
                }
            }
        }

        private void listBoxRecords_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (e.Data.GetDataPresent(typeof(DatabaseTable)))
            {
                DatabaseTable table = e.Data.GetData(typeof(DatabaseTable)) as DatabaseTable;

                var frameworkElement = sender as FrameworkElement;
                if (frameworkElement != null)
                {
                    var levelSpec = frameworkElement.DataContext as LevelSpec;
                    if (levelSpec.Records.FirstOrDefault(r => r.DatabaseTable == table) == null)
                        e.Effects = DragDropEffects.Copy;
                }
            }
        }

        private void listBoxRecords_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(DatabaseTable)))
            {
                DatabaseTable table = e.Data.GetData(typeof(DatabaseTable)) as DatabaseTable;
                var frameworkElement = sender as FrameworkElement;
                if (frameworkElement != null)
                {
                    var levelSpec = frameworkElement.DataContext as LevelSpec;

                    var nextRecordType = (char)((int) LevelSpecs.SelectMany(l => l.Records).Select(r => r.Type).DefaultIfEmpty('0').Max() + 1);
                    if (levelSpec.Records.FirstOrDefault(r => r.DatabaseTable == table) == null)
                        levelSpec.Records.Add(new RecordSpec { Name = table.Name, DatabaseTable = table, Type= nextRecordType});
                }
            }
        }

        private void buttonAddLevel_Click(object sender, RoutedEventArgs e)
        {
            LevelSpecs.Add(new LevelSpec {
                Name = "",
                Number = LevelSpecs.Count + 1,
                IdItems = new ObservableCollection<DatabaseColumn>(),
                Records = new ObservableCollection<RecordSpec>() });
        }

        private void buttonCreateDictionary_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(DictionaryLabel))
            {
                MessageBox.Show("Please enter the dictionary label");
                return;
            }

            if (String.IsNullOrEmpty(DictionaryPath))
            {
                MessageBox.Show("Please enter the name of the file to save the dictionary to");
                return;
            }

            if (LevelSpecs.Count == 0)
            {
                MessageBox.Show("Please add at least one dictionary level");
                return;
            }

            foreach (var levelSpec in LevelSpecs)
            {
                if (levelSpec.IdItems.Count == 0)
                {
                    MessageBox.Show(String.Format("Level {0} does not have any Id Items. Each level must have at least one Id Item.", levelSpec.Name));
                    return;
                }
                if (levelSpec.Records.Count == 0)
                {
                    MessageBox.Show(String.Format("Level {0} does not have any records. Each level must have at least one record.", levelSpec.Name));
                    return;
                }
            }

            try
            {
                DictionaryGenerator generator = new DictionaryGenerator();
                var dictionary = generator.CreateDictionary(DictionaryLabel, DatabaseName, LevelSpecs);

                dictionary.Save(DictionaryPath);

                // Show dictionary file in explorer
                System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + DictionaryPath + "\"");

            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Error creating dictionary: {0}", ex.Message));
            }
        }

        private void buttonDictionaryFilenameBrowse_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSPro Data Dictionary Files (*.dcf)|*.dcf";
            saveFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(DictionaryPath);
            if (saveFileDialog.ShowDialog() == true)
            {
                DictionaryPath = saveFileDialog.FileName;
                NotifyPropertyChanged("DictionaryPath");
            }
        }
    }
}
