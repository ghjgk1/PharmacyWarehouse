using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PharmacyWarehouse.Pages
{
    public partial class CategoriesPage : Page
    {
        private bool _isPlaceholderText = true;
        private string _currentSearchText = "";

        public CategoryService Service { get; set; }
        public Category SelectedCategory { get; set; }
        public string SearchText { get; set; }

        public CategoriesPage()
        {
            InitializeComponent();
            Service = App.ServiceProvider.GetService<CategoryService>();
            DataContext = this;

            LoadCategories();

            _isPlaceholderText = true;
            SearchTextBox.Foreground = Brushes.Gray;

            var isAdmin = AuthService.IsAdmin();
            Admin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadCategories()
        {
            try
            {
                Service.GetAll();

                // Обновляем TreeView
                UpdateTreeView();

                // Обновляем статистику
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTreeView()
        {
            tvCategories.Items.Clear();

            foreach (var category in Service.Categories)
            {
                var categoryNode = new TreeViewItem
                {
                    Header = CreateCategoryHeader(category),
                    Tag = category
                };

                tvCategories.Items.Add(categoryNode);
            }
        }

        private StackPanel CreateCategoryHeader(Category category)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = "📁",
                        Margin = new Thickness(0, 0, 8, 0)
                    },
                    new TextBlock
                    {
                        Text = category.Name,
                        FontWeight = FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = $" ({category.Products?.Count ?? 0})",
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Margin = new Thickness(4, 0, 0, 0)
                    }
                }
            };
        }

        private void UpdateStatistics()
        {
            try
            {
                int totalCategories = Service.Categories.Count;
                int totalProducts = Service.Categories.Sum(c => c.Products?.Count ?? 0);
                double avgProducts = totalCategories > 0 ? (double)totalProducts / totalCategories : 0;

                txtTotalCategories.Text = $"Всего категорий: {totalCategories}";
                txtTotalProducts.Text = $"Товаров в категориях: {totalProducts}";
                txtAvgProducts.Text = $"Среднее товаров: {avgProducts:F1}";
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не показываем пользователю
                Console.WriteLine($"Ошибка статистики: {ex.Message}");
            }
        }

        #region CRUD Operations

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtCategoryName.Text))
                {
                    MessageBox.Show("Введите название категории", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newCategory = new Category
                {
                    Name = txtCategoryName.Text.Trim(),
                    Description = txtCategoryDescription.Text?.Trim()
                };

                Service.Add(newCategory);

                // Очищаем поля
                ClearForm();

                // Обновляем интерфейс
                LoadCategories();

                MessageBox.Show("Категория успешно добавлена", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении категории: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedCategory == null)
                {
                    MessageBox.Show("Выберите категорию для редактирования", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtCategoryName.Text))
                {
                    MessageBox.Show("Введите название категории", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем актуальную категорию из базы
                var categoryToUpdate = Service.GetById(SelectedCategory.Id);
                if (categoryToUpdate == null)
                {
                    MessageBox.Show("Категория не найдена", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Обновляем свойства
                categoryToUpdate.Name = txtCategoryName.Text.Trim();
                categoryToUpdate.Description = txtCategoryDescription.Text?.Trim();

                // Сохраняем изменения
                var result = Service.Commit();

                if (result > 0)
                {
                    // Обновляем в коллекции
                    var index = Service.Categories.IndexOf(SelectedCategory);
                    if (index >= 0)
                    {
                        Service.Categories[index] = categoryToUpdate;
                    }

                    // Обновляем интерфейс
                    LoadCategories();

                    MessageBox.Show("Категория успешно обновлена", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении категории: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SelectedCategory == null)
                {
                    MessageBox.Show("Выберите категорию для удаления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем, есть ли товары в категории
                var categoryWithProducts = Service.GetById(SelectedCategory.Id);
                if (categoryWithProducts?.Products?.Count > 0)
                {
                    MessageBox.Show($"Нельзя удалить категорию, в которой есть товары.\nТоваров в категории: {categoryWithProducts.Products.Count}",
                        "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show($"Удалить категорию '{SelectedCategory.Name}'?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Service.Remove(SelectedCategory);

                    // Очищаем форму
                    ClearForm();

                    // Обновляем интерфейс
                    LoadCategories();

                    MessageBox.Show("Категория успешно удалена", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении категории: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            txtCategoryName.Text = string.Empty;
            txtCategoryDescription.Text = string.Empty;
            SelectedCategory = null;
            dgCategoryProducts.ItemsSource = null;
            txtSelectedCategory.Text = "Категория не выбрана";
        }

        #endregion

        #region TreeView Events

        private void TvCategories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is Category category)
                {
                    SelectedCategory = category;

                    // Заполняем форму данными выбранной категории
                    txtCategoryName.Text = category.Name;
                    txtCategoryDescription.Text = category.Description;

                    // Показываем товары в категории
                    ShowProductsInCategory(category);

                    txtSelectedCategory.Text = $"Категория: {category.Name} (ID: {category.Id})";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при выборе категории: {ex.Message}");
            }
        }

        private void ShowProductsInCategory(Category category)
        {
            try
            {
                var fullCategory = Service.GetById(category.Id);
                if (fullCategory?.Products != null)
                {
                    dgCategoryProducts.ItemsSource = fullCategory.Products;

                    // Обновляем статистику товаров
                    UpdateCategoryProductsInfo(fullCategory);
                }
                else
                {
                    dgCategoryProducts.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCategoryProductsInfo(Category category)
        {
            try
            {
                if (category.Products == null || !category.Products.Any())
                {
                    return;
                }

                // Здесь можно добавить дополнительную статистику
                // Например, общее количество товаров, среднюю цену и т.д.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обновления информации о товарах: {ex.Message}");
            }
        }

        #endregion

        #region Search Functionality

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderText)
            {
                SearchTextBox.Text = "";
                SearchTextBox.Foreground = Brushes.Black;
                _isPlaceholderText = false;
            }
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Поиск по названию или описанию...";
                SearchTextBox.Foreground = Brushes.Gray;
                _isPlaceholderText = true;
                _currentSearchText = "";
            }
            else
            {
                _currentSearchText = SearchTextBox.Text;
                _isPlaceholderText = false;
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderText || string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                MessageBox.Show("Введите текст для поиска", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplySearch();
        }

        private void ApplySearch()
        {
            try
            {
                string searchText;

                if (_isPlaceholderText)
                {
                    searchText = "";
                }
                else
                {
                    searchText = SearchTextBox.Text?.Trim();
                }

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    UpdateTreeView();
                    return;
                }

                var searchTerm = searchText.ToLower();
                var filteredCategories = Service.Categories
                    .Where(c => c.Name.ToLower().Contains(searchTerm) ||
                               (c.Description != null && c.Description.ToLower().Contains(searchTerm)))
                    .ToList();

                UpdateFilteredTreeView(filteredCategories);

                // Показываем результат поиска
                if (filteredCategories.Count == 0)
                {
                    MessageBox.Show("Категории не найдены", "Результат поиска",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateFilteredTreeView(System.Collections.Generic.List<Category> filteredCategories)
        {
            tvCategories.Items.Clear();

            foreach (var category in filteredCategories)
            {
                var categoryNode = new TreeViewItem
                {
                    Header = CreateCategoryHeader(category),
                    Tag = category
                };

                tvCategories.Items.Add(categoryNode);
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "Поиск по названию или описанию...";
            SearchTextBox.Foreground = Brushes.Gray;
            _isPlaceholderText = true;
            _currentSearchText = "";
            UpdateTreeView();

            Keyboard.ClearFocus();
        }

        #endregion

        #region Additional Actions

        private void MoveProducts_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("Выберите категорию", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Функция перемещения товаров из категории '{SelectedCategory.Name}' будет реализована позже",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportList_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция экспорта списка категорий будет реализована позже",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ChangeCategory_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCategory == null)
            {
                MessageBox.Show("Выберите категорию", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show($"Функция смены категории для '{SelectedCategory.Name}' будет реализована позже",
                "В разработке", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadCategories();
        }
    }
}
