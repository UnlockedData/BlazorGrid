using BlazorGrid.Abstractions;
using BlazorGrid.Abstractions.Filters;
using BlazorGrid.Abstractions.Helpers;
using BlazorGrid.Interfaces;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BlazorGrid.Components
{
    public partial class BlazorGrid<TRow> : IDisposable, IBlazorGrid where TRow : class
    {
        [Inject] public IGridProvider Provider { get; set; }
        [Inject] public NavigationManager Nav { get; set; }

        private bool IsLoadingMore { get; set; }
        public const int DefaultPageSize = 25;

        [Parameter] public string SourceUrl { get; set; }
        [Parameter] public RenderFragment<TRow> ChildContent { get; set; }
        [Parameter] public int PageSize { get; set; } = DefaultPageSize;

        private string _Query;

        [Parameter]
        public string Query
        {
            get => _Query;
            set
            {

                if (_Query == value)
                    return;

                _Query = value;

                InvokeAsync(async () =>
                {
                    await Task.Delay(300);

                    if (_Query == value)
                    {
                        QueryDebounced = value;
                        await LoadAsync(true);
                    }
                });

            }
        }

        [Parameter] public EventCallback<TRow> OnClick { get; set; }
        [Parameter] public Func<TRow, string> Href { get; set; }
        [Parameter] public Expression<Func<TRow, object>> DefaultOrderBy { get; set; }
        [Parameter] public bool DefaultOrderByDescending { get; set; }
        [Parameter] public List<TRow> Rows { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public IDictionary<string, object> Attributes { get; set; }

        public FilterDescriptor Filter { get; private set; } = new FilterDescriptor();

        private string QueryDebounced { get; set; }
        public string OrderByPropertyName { get; private set; }
        public bool OrderByDescending { get; private set; }
        private int TotalCount { get; set; }
        private IList<IGridCol> ColumnsList { get; set; } = new List<IGridCol>();
        public IEnumerable<IGridCol> Columns => ColumnsList;
        private Exception LoadingError { get; set; }

        public event EventHandler<int> OnAfterRowClicked;

        private IDictionary<string, object> FinalAttributes
        {
            get
            {
                var attr = new Dictionary<string, object>
                {
                    { "class", CssClass }
                };

                if (Attributes != null)
                {
                    foreach (var a in Attributes)
                    {
                        if (a.Key != "class")
                        {
                            attr.Add(a.Key, a.Value);
                        }
                    }
                }

                return attr;
            }
        }

        public string CssClass
        {
            get
            {
                var cls = new List<string> { "blazor-grid" };

                if (Attributes != null)
                {
                    string customClasses = Attributes
                        .Where(x => x.Key == "class")
                        .Select(x => x.Value?.ToString())
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(customClasses))
                    {
                        // Merge custom classes
                        cls.AddRange(customClasses.Split(' '));
                    }
                }

                return string.Join(' ', cls).Trim();
            }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                if (DefaultOrderBy != null)
                {
                    OrderByPropertyName = ExpressionHelper.GetPropertyName(DefaultOrderBy);
                    OrderByDescending = DefaultOrderByDescending;
                }

                if (Rows == null)
                {
                    InvokeAsync(() => LoadAsync(true));
                }

                // Subscribe to Filter object changes
                Filter.PropertyChanged += OnFilterChanged;
                Filter.Filters.CollectionChanged += OnFilterCollectionChanged;
            }
        }

        private void OnFilterCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Reload();
        private void OnFilterChanged(object sender, PropertyChangedEventArgs e) => Reload();

        public Task Reload()
        {
            return LoadAsync(true);
        }

        private async Task LoadAsync(bool Initialize)
        {
            if (IsLoadingMore)
            {
                return;
            }

            if (Initialize)
            {
                Rows = null;
            }

            LoadingError = null;
            IsLoadingMore = true;
            StateHasChanged();

            try
            {
                var result = await Provider.GetAsync<TRow>(
                    SourceUrl,
                    Rows?.Count ?? 0,
                    PageSize,
                    OrderByPropertyName,
                    OrderByDescending,
                    QueryDebounced,
                    Filter
                );

                if (result != null)
                {
                    TotalCount = result.TotalCount;

                    if (Initialize)
                    {
                        Rows = result.Data.ToList();
                    }
                    else
                    {
                        Rows.AddRange(result.Data);
                    }
                }
            }
            catch (Exception x)
            {
                LoadingError = x;
            }
            finally
            {
                IsLoadingMore = false;
                StateHasChanged();
            }
        }

        public Task TryApplySorting(string PropertyName)
        {
            if (string.IsNullOrEmpty(PropertyName))
                return Task.CompletedTask;

            if (OrderByPropertyName == PropertyName)
            {
                OrderByDescending = !OrderByDescending;
            }
            else
            {
                OrderByPropertyName = PropertyName;
                OrderByDescending = false;
            }

            return LoadAsync(true);
        }

        private string GridColumns
        {
            get
            {
                var sizes = ColumnsList.Select(col => col.FitToContent ? "max-content" : "auto");
                return string.Join(' ', sizes);
            }
        }

        public Task LoadMoreAsync()
        {
            if (IsLoadingMore)
            {
                return Task.CompletedTask;
            }

            return InvokeAsync(() => LoadAsync(false));
        }

        public int LastClickedRowIndex { get; private set; } = -1;
        private void OnRowClicked(TRow r, int index)
        {
            if (r == null)
            {
                return;
            }

            LastClickedRowIndex = index;
            var onClickUrl = Href?.Invoke(r);

            if (onClickUrl != null)
            {
                Nav.NavigateTo(onClickUrl);
            }
            else if (OnClick.HasDelegate)
            {
                OnClick.InvokeAsync(r);
            }

            OnAfterRowClicked?.Invoke(this, index);
        }

        public void Add(IGridCol col)
        {
            ColumnsList.Add(col);
            StateHasChanged();
        }

        PropertyType IBlazorGrid.GetTypeFor(string PropertyName)
        {
            return GetTypeFor(PropertyName);
        }

        public static PropertyType GetTypeFor(string PropertyName)
        {
            var t = typeof(TRow);
            var p = t.GetProperty(PropertyName);

            if (p == null)
            {
                throw new ArgumentException(string.Format("Property {0} was not found on type {1}", PropertyName, t.Name));
            }

            t = p.PropertyType;

            var intType = typeof(int);

            if (intType.IsAssignableFrom(t) || intType.IsAssignableFrom(Nullable.GetUnderlyingType(t)))
            {
                return PropertyType.Integer;
            }

            var decType = typeof(decimal);

            if (decType.IsAssignableFrom(t) || decType.IsAssignableFrom(Nullable.GetUnderlyingType(t)))
            {
                return PropertyType.Decimal;
            }

            return PropertyType.String;
        }

        public bool IsFilteredBy(string PropertyName)
        {
            return Filter?.Filters.Any(x => x.Property == PropertyName) == true;
        }

        public void Dispose()
        {
            if (Filter != null)
            {
                Filter.PropertyChanged -= OnFilterChanged;

                if (Filter.Filters != null)
                {
                    Filter.Filters.CollectionChanged -= OnFilterCollectionChanged;
                }
            }
        }
    }
}