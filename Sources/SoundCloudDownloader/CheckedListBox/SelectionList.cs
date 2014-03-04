using System.Collections.Generic;
using System.ComponentModel;

namespace SoundCloudDownloader {

    // From: http://www.mostefaiamine.com/post/2013/12/13/Create-a-Multi-Checkbox-List-in-WPF
    public class SelectionList<T>: List<SelectionItem<T>>, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private int selectionCount;

        /// <summary>
        /// Gets the number of selected elements.
        /// </summary>
        /// <value>
        /// The number of selected elements.
        /// </value>
        public int SelectionCount {
            get {
                return this.selectionCount;
            }
            private set {
                this.selectionCount = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("SelectionCount"));
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionList{T}"/> class.
        /// </summary>
        public SelectionList() {
        }

        /// <summary>
        /// Creates the selection list from an existing simple list.
        /// </summary>
        public SelectionList(IEnumerable<T> elements) {
            foreach (T element in elements) {
                AddItem(element);
            }
        }

        /// <summary>
        /// Adds an element to the element and listens to its "IsSelected" property to update the
        /// SelectionCount property. Use this method instead of the "Add" one.
        /// </summary>
        public void AddItem(T element) {
            var item = new SelectionItem<T>(element);
            item.PropertyChanged += item_PropertyChanged;
            Add(item);
        }

        private void item_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            var item = sender as SelectionItem<T>;
            if (( item != null ) && e.PropertyName == "IsSelected") {
                if (item.IsSelected) {
                    SelectionCount += SelectionCount;
                } else {
                    SelectionCount -= SelectionCount;
                }
            }
        }
    }
}