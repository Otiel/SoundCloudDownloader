using System;
using System.ComponentModel;

namespace SoundCloudDownloader {

    // From: http://www.mostefaiamine.com/post/2013/12/13/Create-a-Multi-Checkbox-List-in-WPF
    public class SelectionItem<T>: INotifyPropertyChanged {

        /// <summary>
        /// Indicates if the item is selected.
        /// </summary>
        private Boolean isSelected;

        /// <summary>
        /// Gets or sets a value indicating whether the element is selected.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the element is selected; otherwise, <c>false</c>.
        /// </value>
        public Boolean IsSelected {
            get {
                return isSelected;
            }
            set {
                isSelected = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("IsSelected"));
            }
        }

        /// <summary>
        /// Gets or sets the element.
        /// </summary>
        /// <value>
        /// The element.
        /// </value>
        public T Element { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionItem{T}"/> class.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="isSelected">if set to <c>true</c>, the element will be selected.</param>
        public SelectionItem(T element, Boolean isSelected) {
            Element = element;
            IsSelected = IsSelected;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionItem{T}"/> class.
        /// </summary>
        /// <param name="element">The element.</param>
        public SelectionItem(T element)
            : this(element, false) {
        }
    }
}