import { Component, OnInit, Input, HostBinding, ViewChild } from '@angular/core';
import { DetailsPickerComponent } from '../details-picker/details-picker.component';

@Component({
  template: ''
})
export class PickerBaseComponent {

  @Input()
  showCreate = true;

  @Input()
  filter: string;

  @Input()
  additionalSelect: string;

  @HostBinding('class.w-100')
  w100 = true;

  @ViewChild(DetailsPickerComponent, { static: true })
  picker: DetailsPickerComponent;

  writeValue(obj: any): void {
    this.picker.writeValue(obj);
  }
  registerOnChange(fn: any): void {
    this.picker.registerOnChange(fn);
  }
  registerOnTouched(fn: any): void {
    this.picker.registerOnTouched(fn);
  }
  setDisabledState?(isDisabled: boolean): void {
    this.picker.setDisabledState(isDisabled);
  }

}
