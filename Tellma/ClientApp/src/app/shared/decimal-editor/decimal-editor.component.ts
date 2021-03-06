import { Component, ViewChild, ElementRef, Input, HostBinding, OnChanges, SimpleChanges } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 't-decimal-editor',
  templateUrl: './decimal-editor.component.html',
  providers: [{ provide: NG_VALUE_ACCESSOR, multi: true, useExisting: DecimalEditorComponent }]
})
export class DecimalEditorComponent implements ControlValueAccessor, OnChanges {

  @Input()
  textAlignment: 'left' | 'right' = null;

  @Input()
  maxDecimalPlaces: number = null;

  @Input()
  minDecimalPlaces: number = null;

  @ViewChild('input', { static: true })
  input: ElementRef;

  @HostBinding('class.w-100')
  w100 = true;

  ///////////////// Implementation of ControlValueAccessor
  public isDisabled = false;
  public onChange: (val: any) => void = _ => { };
  public onTouched: () => void = () => { };
  public onValidatorChange: () => void = () => { };

  writeValue(v: any): void {

    this.input.nativeElement.value = this.format(v); // Format
  }

  registerOnChange(fn: (val: any) => void): void {
    this.onChange = (val) => {
      fn(this.parse(val));
    };
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!!changes.maxDecimalPlaces && !changes.maxDecimalPlaces.isFirstChange()
      || !!changes.minDecimalPlaces && !changes.minDecimalPlaces.isFirstChange()) {
      this.input.nativeElement.value = this.format(this.input.nativeElement.value);
    }
  }


  ///////////////// Helper methods: Copied from the Internet and modified
  ///////////////// Credit goes to https://bit.ly/2BV9oy3

  private get padding() {
    return '00000000';
  }

  private get decimalSeparator() {
    // TODO Determine based on current culture
    return '.';
  }

  private get thousandsSeparator() {
    // TODO Determine based on current culture
    return ',';
  }

  private format(value: number | string): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    // Takes a number and formats it with a decimal point and a thousands separator
    let [integer, fraction = ''] = (value || '0').toString()
      .split(this.decimalSeparator);

    let min = this.minDecimalPlaces;
    if (min === null) {
      min = fraction.length;
    }

    let max = Math.max(this.maxDecimalPlaces, min);
    if (max === null) {
      max = fraction.length;
    }

    let fractionSize = fraction.length;
    fractionSize = Math.max(min, fractionSize);
    fractionSize = Math.min(max, fractionSize);

    fraction = fractionSize > 0
      ? this.decimalSeparator + (fraction + this.padding).substring(0, fractionSize)
      : '';

    integer = integer.replace(/\B(?=(\d{3})+(?!\d))/g, this.thousandsSeparator);

    return integer + fraction;
  }

  private parse(value: string): number {

    if (value === null || value === undefined || value === '') {
      return undefined;
    }

    // Reverses the effect of 'format' method above, and returns the original number
    let [integer, fraction = ''] = (value || '').split(this.decimalSeparator);

    integer = integer.replace(new RegExp(this.thousandsSeparator, 'g'), '');

    let fractionSize = this.maxDecimalPlaces;
    if (fractionSize === null) {
      fractionSize = fraction.length;
    }

    fraction = parseInt(fraction, 10) > 0 && fractionSize > 0
      ? this.decimalSeparator + (fraction + this.padding).substring(0, fractionSize)
      : '';

    const stringResult = integer + fraction;
    return !!stringResult ? +stringResult : 0;
  }

  ////////////////// Behavior
  onBlur(value: any) {

    // signal Angular forms, that the element was touched
    this.onTouched();

    // always format the input on blur
    this.input.nativeElement.value = this.format(this.parse(value));
  }
}
